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

    public async Task<EmailSendResult> EmailInvoiceAsync(
        InvoiceFormat format,
        InvoiceDocument invoice,
        CreateInvoiceOptions? options,
        EmailMessage email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(email);

        var requestBody = new CreateInvoiceEmailRequest
        {
            Invoice = invoice,
            Options = options ?? new CreateInvoiceOptions(),
            Email = email,
        };

        var path = $"{ApiVersionPrefix}/create/{Slug(format)}/email";
        using var response = await _http.PostAsJsonAsync(
            path, requestBody, InvoiceXmlJsonOptions.Default, cancellationToken).ConfigureAwait(false);

        return await ReadEmailResultAsync(response, cancellationToken).ConfigureAwait(false);
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
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
            throw new ArgumentException("Content bytes must not be empty.", nameof(content));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return ValidateAsync(EnumWire.Slug(ValidationReportFormat.OrderX), content, contentType, fileName, rules: null,
            validationLanguage, cancellationToken);
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
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);
        return ValidateAsync(Slug(format), xmlBytes, "application/xml", "invoice.xml", rules, validationLanguage, cancellationToken);
    }

    public Task<ValidationResult> ValidatePdfAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        IReadOnlyList<ExtraRuleset>? rules,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));

        return ValidateAsync(Slug(format), pdf, "application/pdf", "invoice.pdf", rules, validationLanguage, cancellationToken);
    }

    private async Task<ValidationResult> ValidateAsync(
        string slug,
        byte[] content,
        string contentType,
        string fileName,
        IReadOnlyList<ExtraRuleset>? rules,
        ValidationLanguage? validationLanguage,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        AddRules(form, rules);
        AddValidationLanguage(form, validationLanguage);

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
        ValidationLanguage? validationLanguage = null,
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
        AddValidationLanguage(form, validationLanguage);

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
        string? logo,
        FooterBrand? footerBrand,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var form = new MultipartFormDataContent();
        form.Add(FilePart(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml"), "file", "invoice.xml");
        // The API's default language is en.
        form.Add(new StringContent(Code(language)), "language");
        // The API fetches a logoUrl itself (SSRF-guarded, re-encoded), so only the URL travels.
        if (!string.IsNullOrWhiteSpace(logoUrl))
            form.Add(new StringContent(logoUrl), "logoUrl");
        // The image itself goes as the logo file part; the API gates it by signature, so the
        // part's own type only has to be an image type.
        if (!string.IsNullOrWhiteSpace(logo))
        {
            var bytes = Convert.FromBase64String(logo);
            var png = bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
            form.Add(FilePart(bytes, png ? "image/png" : "image/jpeg"), "logo", png ? "logo.png" : "logo.jpg");
        }
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
        ValidationLanguage? validationLanguage = null,
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
        AddValidationLanguage(form, validationLanguage);

        var path = $"{ApiVersionPrefix}/embed/{Slug(format)}";
        return SendForArtifactAsync(path, form, $"invoice-{Slug(format)}.pdf", cancellationToken);
    }

    public async Task<EmailSendResult> EmailEmbeddedAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        string ciiXml,
        EmailMessage email,
        IReadOnlyList<ExtraRuleset>? rules,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));
        ArgumentException.ThrowIfNullOrWhiteSpace(ciiXml);
        ArgumentNullException.ThrowIfNull(email);

        using var form = new MultipartFormDataContent();
        form.Add(FilePart(pdf, "application/pdf"), "pdf", "invoice.pdf");
        form.Add(FilePart(System.Text.Encoding.UTF8.GetBytes(ciiXml), "application/xml"), "xml", "invoice.xml");
        AddRules(form, rules);
        AddValidationLanguage(form, validationLanguage);
        AddEmail(form, email);

        var path = $"{ApiVersionPrefix}/embed/{Slug(format)}/email";
        using var response = await _http.PostAsync(path, form, cancellationToken).ConfigureAwait(false);

        return await ReadEmailResultAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public Task<DocumentArtifact> ConvertAsync(
        InvoiceFormat source,
        InvoiceFormat target,
        byte[] content,
        string contentType,
        string fileName,
        PdfLanguage? language,
        FooterBrand? footerBrand,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
            throw new ArgumentException("Content bytes must not be empty.", nameof(content));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var form = new MultipartFormDataContent();
        form.Add(FilePart(content, contentType), "file", fileName);
        AddValidationLanguage(form, validationLanguage);

        // Only the render-and-embed promotions (XML to a hybrid PDF) render a PDF face, so
        // only they take its language and footer. The language is omitted when unset so
        // the API's own default (English) applies.
        var ext = target is InvoiceFormat.FacturX or InvoiceFormat.Zugferd ? "pdf" : "xml";
        if (ext == "pdf")
        {
            if (language is { } pdfLanguage)
                form.Add(new StringContent(Code(pdfLanguage)), "language");
            AddFooterBrand(form, footerBrand);
        }

        var path = $"{ApiVersionPrefix}/convert/{Slug(source)}/to/{Slug(target)}";
        return SendForArtifactAsync(path, form, $"invoice-{Slug(target)}.{ext}", cancellationToken);
    }

    // The transform language that lets the API follow the language the invoice is written in.
    internal const string AutoTransformLanguage = "auto";

    public Task<DocumentArtifact> TransformAsync(
        InvoiceFormat target,
        byte[] pdf,
        PdfLanguage? language,
        bool preserveOriginalPdf,
        bool includeAdvancedProperties,
        string? buyerReference,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));

        var form = new MultipartFormDataContent();
        form.Add(FilePart(pdf, "application/pdf"), "file", "invoice.pdf");
        AddValidationLanguage(form, validationLanguage);

        // The XRechnung route needs an explicit buyer reference (BT-10); the hybrid-PDF
        // routes take a language for the pages and the XML's default texts (auto lets the
        // detected language decide), a switch that keeps the uploaded pages, omitted when
        // off so the API's default applies, and the advanced-details switch, always sent so
        // the tool's own default holds whatever the API's is. Other routes ignore extra fields.
        if (target is InvoiceFormat.XRechnung)
        {
            form.Add(new StringContent(buyerReference ?? string.Empty), "buyerReference");
        }
        else if (target is InvoiceFormat.FacturX or InvoiceFormat.Zugferd)
        {
            form.Add(new StringContent(language is { } pdfLanguage ? Code(pdfLanguage) : AutoTransformLanguage), "language");
            if (preserveOriginalPdf)
                form.Add(new StringContent("true"), "preserveOriginalPdf");
            form.Add(new StringContent(includeAdvancedProperties ? "true" : "false"), "includeAdvancedProperties");
        }

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

    // Omitted when unset so the API's own default (English) stays in charge.
    private static void AddValidationLanguage(MultipartFormDataContent form, ValidationLanguage? validationLanguage)
    {
        if (validationLanguage is { } language)
            form.Add(new StringContent(EnumWire.Slug(language)), "validationLanguage");
    }

    // The embed email routes take the email as plain form fields; cc and bcc repeat per address.
    private static void AddEmail(MultipartFormDataContent form, EmailMessage email)
    {
        AddField(form, "email.from", email.From);
        AddField(form, "email.fromName", email.FromName);
        AddField(form, "email.to", email.To);
        foreach (var address in email.Cc ?? [])
            AddField(form, "email.cc", address);
        foreach (var address in email.Bcc ?? [])
            AddField(form, "email.bcc", address);
        AddField(form, "email.replyTo", email.ReplyTo);
        AddField(form, "email.subject", email.Subject);
        AddField(form, "email.message", email.Message);
    }

    private static void AddField(MultipartFormDataContent form, string name, string? value)
    {
        if (value is not null)
            form.Add(new StringContent(value), name);
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

    private static async Task<EmailSendResult> ReadEmailResultAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content
            .ReadFromJsonAsync<EmailSendResult>(InvoiceXmlJsonOptions.Default, cancellationToken)
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException(
            "InvoiceXML API returned an empty email response.");
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

    // The API's language form fields take lower-case codes (en / de / fr).
    private static string Code(PdfLanguage language) => language.ToString().ToLowerInvariant();

    private static string DefaultFileName(InvoiceFormat format) => format switch
    {
        InvoiceFormat.FacturX or InvoiceFormat.Zugferd => $"invoice-{Slug(format)}.pdf",
        _ => $"invoice-{Slug(format)}.xml",
    };
}
