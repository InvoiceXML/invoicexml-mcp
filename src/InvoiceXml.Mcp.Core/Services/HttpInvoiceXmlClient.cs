using System.Net.Http.Headers;
using System.Net.Http.Json;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;

namespace InvoiceXml.Mcp.Core.Services;

/// <summary>
/// Default <see cref="IInvoiceXmlClient"/> implementation. Translates the typed
/// method calls into HTTP requests against the InvoiceXML API; authentication
/// is supplied by host-side <see cref="DelegatingHandler"/>s, not by this class.
/// </summary>
internal sealed class HttpInvoiceXmlClient : IInvoiceXmlClient
{
    private const string ApiVersionPrefix = "v1";

    private readonly HttpClient _http;

    public HttpInvoiceXmlClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<CreateInvoiceResult> CreateInvoiceAsync(
        InvoiceFormat format,
        InvoiceDocument invoice,
        CreateInvoiceOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var requestBody = new CreateInvoiceRequest
        {
            Invoice = invoice,
            Options = options ?? new CreateInvoiceOptions(),
        };

        var path = $"{ApiVersionPrefix}/create/{Slug(format)}";
        using var response = await _http.PostAsJsonAsync(
            path, requestBody, InvoiceXmlJsonOptions.Default, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var artifact = await ReadArtifactAsync(response, DefaultFileName(format), cancellationToken).ConfigureAwait(false);

        return new CreateInvoiceResult
        {
            Content = artifact.Content,
            ContentType = artifact.ContentType,
            FileName = artifact.FileName,
        };
    }

    public async Task<DocumentArtifact> CreateOrderAsync(
        OrderFormat format,
        InvoiceDocument order,
        CreateOrderOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var requestBody = new CreateOrderRequest
        {
            Order = order,
            Options = options ?? new CreateOrderOptions(),
        };

        var slug = EnumWire.Slug(format);
        var path = $"{ApiVersionPrefix}/create/{slug}";
        using var response = await _http.PostAsJsonAsync(
            path, requestBody, InvoiceXmlJsonOptions.Default, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var defaultName = format is OrderFormat.OrderX ? "order-x.pdf" : "order-cio.xml";
        return await ReadArtifactAsync(response, defaultName, cancellationToken).ConfigureAwait(false);
    }

    public Task<ValidationResult> ValidateOrderAsync(
        byte[] content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
            throw new ArgumentException("Content bytes must not be empty.", nameof(content));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return ValidateAsync(EnumWire.Slug(ValidationReportFormat.OrderX), content, contentType, fileName, rules: null, cancellationToken);
    }

    public async Task<AccountInfo> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"{ApiVersionPrefix}/me", cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var account = await response.Content
            .ReadFromJsonAsync<AccountInfo>(InvoiceXmlJsonOptions.Default, cancellationToken)
            .ConfigureAwait(false);

        return account ?? throw new InvalidOperationException(
            "InvoiceXML API returned an empty account response.");
    }

    public Task<ValidationResult> ValidateXmlAsync(
        XmlInvoiceFormat format,
        string xml,
        IReadOnlyList<ExtraRuleset>? rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);
        return ValidateAsync(Slug(format), xmlBytes, "application/xml", "invoice.xml", rules, cancellationToken);
    }

    public Task<ValidationResult> ValidatePdfAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        IReadOnlyList<ExtraRuleset>? rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));

        return ValidateAsync(Slug(format), pdf, "application/pdf", "invoice.pdf", rules, cancellationToken);
    }

    private async Task<ValidationResult> ValidateAsync(
        string slug,
        byte[] content,
        string contentType,
        string fileName,
        IReadOnlyList<ExtraRuleset>? rules,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        AddRules(form, rules);

        var path = $"{ApiVersionPrefix}/validate/{slug}";
        using var response = await _http.PostAsync(path, form, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content
            .ReadFromJsonAsync<ValidationResult>(InvoiceXmlJsonOptions.Default, cancellationToken)
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException(
            "InvoiceXML API returned an empty validation response.");
    }

    public async Task<ValidationReportPdfResult> ValidationReportPdfAsync(
        ValidationReportFormat format,
        byte[] content,
        string contentType,
        string fileName,
        IReadOnlyList<ExtraRuleset>? rules,
        FooterBrand? footerBrand,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
            throw new ArgumentException("Content bytes must not be empty.", nameof(content));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        using var form = new MultipartFormDataContent();
        form.Add(FilePart(content, contentType), "file", fileName);
        AddRules(form, rules);
        AddFooterBrand(form, footerBrand);

        var path = $"{ApiVersionPrefix}/validate/{EnumWire.Slug(format)}/report";
        using var response = await _http.PostAsync(path, form, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var artifact = await ReadArtifactAsync(
            response, $"validation-report-{EnumWire.Slug(format)}.pdf", cancellationToken).ConfigureAwait(false);

        // The verdict rides in a response header so callers can branch on
        // validity without parsing the PDF; absent or unreadable means unknown.
        bool? valid = null;
        if (response.Headers.TryGetValues("X-Invoice-Valid", out var values)
            && bool.TryParse(values.FirstOrDefault(), out var parsed))
        {
            valid = parsed;
        }

        return new ValidationReportPdfResult { Report = artifact, Valid = valid };
    }

    public Task<DocumentArtifact> RenderToPdfAsync(
        XmlInvoiceFormat format,
        string xml,
        PdfLanguage language,
        string? logoUrl,
        FooterBrand? footerBrand,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var form = new MultipartFormDataContent();
        form.Add(FilePart(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml"), "file", "invoice.xml");
        // API form field is a lower-case language code (en / de / fr); default is en.
        form.Add(new StringContent(language.ToString().ToLowerInvariant()), "language");
        // The API fetches the logo itself (SSRF-guarded, re-encoded), so only the URL travels.
        if (!string.IsNullOrWhiteSpace(logoUrl))
            form.Add(new StringContent(logoUrl), "logoUrl");
        AddFooterBrand(form, footerBrand);

        var path = $"{ApiVersionPrefix}/render/{Slug(format)}/to/pdf";
        return SendForArtifactAsync(path, form, "rendered-invoice.pdf", cancellationToken);
    }

    public Task<DocumentArtifact> ExtractAsync(
        ExtractTarget target,
        byte[] content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
            throw new ArgumentException("Content bytes must not be empty.", nameof(content));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var form = new MultipartFormDataContent();
        form.Add(FilePart(content, contentType), "file", fileName);

        var slug = target.ToString().ToLowerInvariant();
        var path = $"{ApiVersionPrefix}/extract/{slug}";
        var defaultName = target switch
        {
            ExtractTarget.Json => "invoice.json",
            ExtractTarget.Attachments => "invoice-attachments.zip",
            _ => "invoice.xml",
        };
        return SendForArtifactAsync(path, form, defaultName, cancellationToken);
    }

    public Task<DocumentArtifact> EmbedAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        string ciiXml,
        IReadOnlyList<ExtraRuleset>? rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));
        ArgumentException.ThrowIfNullOrWhiteSpace(ciiXml);

        var form = new MultipartFormDataContent();
        form.Add(FilePart(pdf, "application/pdf"), "pdf", "invoice.pdf");
        form.Add(FilePart(System.Text.Encoding.UTF8.GetBytes(ciiXml), "application/xml"), "xml", "invoice.xml");
        AddRules(form, rules);

        var path = $"{ApiVersionPrefix}/embed/{Slug(format)}";
        return SendForArtifactAsync(path, form, $"invoice-{Slug(format)}.pdf", cancellationToken);
    }

    public Task<DocumentArtifact> ConvertAsync(
        InvoiceFormat source,
        InvoiceFormat target,
        byte[] content,
        string contentType,
        string fileName,
        FooterBrand? footerBrand,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
            throw new ArgumentException("Content bytes must not be empty.", nameof(content));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var form = new MultipartFormDataContent();
        form.Add(FilePart(content, contentType), "file", fileName);

        // Only the render-and-embed promotions (XML to a hybrid PDF) print a footer.
        var ext = target is InvoiceFormat.FacturX or InvoiceFormat.Zugferd ? "pdf" : "xml";
        if (ext == "pdf")
            AddFooterBrand(form, footerBrand);

        var path = $"{ApiVersionPrefix}/convert/{Slug(source)}/to/{Slug(target)}";
        return SendForArtifactAsync(path, form, $"invoice-{Slug(target)}.{ext}", cancellationToken);
    }

    public Task<DocumentArtifact> TransformAsync(
        InvoiceFormat target,
        byte[] pdf,
        PdfLanguage language,
        string? buyerReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));

        var form = new MultipartFormDataContent();
        form.Add(FilePart(pdf, "application/pdf"), "file", "invoice.pdf");

        // The XRechnung route needs an explicit buyer reference (BT-10); the hybrid-PDF
        // routes take a language for the visual face. Other routes ignore extra fields.
        if (target is InvoiceFormat.XRechnung)
            form.Add(new StringContent(buyerReference ?? string.Empty), "buyerReference");
        else if (target is InvoiceFormat.FacturX or InvoiceFormat.Zugferd)
            form.Add(new StringContent(language.ToString().ToLowerInvariant()), "language");

        var path = $"{ApiVersionPrefix}/transform/to/{Slug(target)}";
        var ext = target is InvoiceFormat.FacturX or InvoiceFormat.Zugferd ? "pdf" : "xml";
        return SendForArtifactAsync(path, form, $"invoice-{Slug(target)}.{ext}", cancellationToken);
    }

    // The API binds 'rules' as a string array: one form part per value.
    private static void AddRules(MultipartFormDataContent form, IReadOnlyList<ExtraRuleset>? rules)
    {
        if (rules is null)
            return;
        foreach (var rule in rules.Distinct())
            form.Add(new StringContent(EnumWire.Slug(rule)), "rules");
    }

    // Omitted when unset so the API's own default credit stays in charge.
    private static void AddFooterBrand(MultipartFormDataContent form, FooterBrand? footerBrand)
    {
        if (footerBrand is { } brand)
            form.Add(new StringContent(EnumWire.Slug(brand)), "footerBrand");
    }

    private static ByteArrayContent FilePart(byte[] bytes, string contentType)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return part;
    }

    // Posts a multipart form and reads the binary/textual artifact the API returns.
    // Disposes the form and response; the artifact bytes are fully buffered first.
    private async Task<DocumentArtifact> SendForArtifactAsync(
        string path, MultipartFormDataContent form, string defaultFileName, CancellationToken cancellationToken)
    {
        using (form)
        {
            using var response = await _http.PostAsync(path, form, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            return await ReadArtifactAsync(response, defaultFileName, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<DocumentArtifact> ReadArtifactAsync(
        HttpResponseMessage response, string defaultFileName, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? defaultFileName;

        return new DocumentArtifact
        {
            Content = content,
            ContentType = contentType,
            FileName = fileName,
        };
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Surface the API's ProblemDetails / error JSON in the exception so callers
        // (and ultimately the LLM) get a readable reason instead of "500".
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            body = "(unable to read response body)";
        }

        throw new InvoiceXmlApiException(response.StatusCode, body);
    }

    private static string Slug(InvoiceFormat format) => format.ToString().ToLowerInvariant();
    private static string Slug(XmlInvoiceFormat format) => format.ToString().ToLowerInvariant();
    private static string Slug(PdfInvoiceFormat format) => format.ToString().ToLowerInvariant();

    private static string DefaultFileName(InvoiceFormat format) => format switch
    {
        InvoiceFormat.FacturX or InvoiceFormat.Zugferd => $"invoice-{Slug(format)}.pdf",
        _ => $"invoice-{Slug(format)}.xml",
    };
}
