using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
        PdfRenderOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var requestBody = new CreateInvoiceRequest
        {
            Invoice = invoice,
            Options = options ?? new PdfRenderOptions(),
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

    public Task<ValidationResult> ValidateXmlAsync(
        XmlInvoiceFormat format,
        string xml,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(xml);
        return ValidateAsync(Slug(format), xmlBytes, "application/xml", "invoice.xml", cancellationToken);
    }

    public Task<ValidationResult> ValidatePdfAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));

        return ValidateAsync(Slug(format), pdf, "application/pdf", "invoice.pdf", cancellationToken);
    }

    private async Task<ValidationResult> ValidateAsync(
        string slug,
        byte[] content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        var path = $"{ApiVersionPrefix}/validate/{slug}";
        using var response = await _http.PostAsync(path, form, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var result = await response.Content
            .ReadFromJsonAsync<ValidationResult>(InvoiceXmlJsonOptions.Default, cancellationToken)
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException(
            "InvoiceXML API returned an empty validation response.");
    }

    public Task<DocumentArtifact> RenderToPdfAsync(
        XmlInvoiceFormat format,
        string xml,
        PdfLanguage language,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        var form = new MultipartFormDataContent();
        form.Add(FilePart(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml"), "file", "invoice.xml");
        // API form field is a lower-case language code (en / de / fr); default is en.
        form.Add(new StringContent(language.ToString().ToLowerInvariant()), "language");

        var path = $"{ApiVersionPrefix}/render/{Slug(format)}/to/pdf";
        return SendForArtifactAsync(path, form, "rendered-invoice.pdf", cancellationToken);
    }

    public Task<DocumentArtifact> ExtractAsync(
        ExtractTarget target,
        byte[] pdf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));

        var form = new MultipartFormDataContent();
        form.Add(FilePart(pdf, "application/pdf"), "file", "invoice.pdf");

        var slug = target.ToString().ToLowerInvariant();
        var path = $"{ApiVersionPrefix}/extract/{slug}";
        var defaultName = target == ExtractTarget.Json ? "invoice.json" : "invoice.xml";
        return SendForArtifactAsync(path, form, defaultName, cancellationToken);
    }

    public Task<DocumentArtifact> EmbedAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        string ciiXml,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0)
            throw new ArgumentException("PDF bytes must not be empty.", nameof(pdf));
        ArgumentException.ThrowIfNullOrWhiteSpace(ciiXml);

        var form = new MultipartFormDataContent();
        form.Add(FilePart(pdf, "application/pdf"), "pdf", "invoice.pdf");
        form.Add(FilePart(System.Text.Encoding.UTF8.GetBytes(ciiXml), "application/xml"), "xml", "invoice.xml");

        var path = $"{ApiVersionPrefix}/embed/{Slug(format)}";
        return SendForArtifactAsync(path, form, $"invoice-{Slug(format)}.pdf", cancellationToken);
    }

    public Task<DocumentArtifact> ConvertAsync(
        InvoiceFormat source,
        InvoiceFormat target,
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

        var path = $"{ApiVersionPrefix}/convert/{Slug(source)}/to/{Slug(target)}";
        var ext = target is InvoiceFormat.FacturX or InvoiceFormat.Zugferd ? "pdf" : "xml";
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
