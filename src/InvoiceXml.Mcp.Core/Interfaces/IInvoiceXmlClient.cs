using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Models;

namespace InvoiceXml.Mcp.Core.Interfaces;

/// <summary>
/// Typed client over the InvoiceXML public API. Tools depend on this contract
/// rather than constructing <see cref="System.Net.Http.HttpClient"/> directly,
/// which keeps tools transport-agnostic and trivially mockable in tests.
/// </summary>
/// <remarks>
/// Authentication is applied transparently by the host through an
/// <see cref="System.Net.Http.DelegatingHandler"/>; implementations of this
/// interface must not concern themselves with credentials.
/// </remarks>
public interface IInvoiceXmlClient
{
    /// <summary>
    /// Calls <c>POST /v1/create/{format}</c> with the supplied document.
    /// Returns the API's binary response together with content-type and filename.
    /// </summary>
    Task<CreateInvoiceResult> CreateInvoiceAsync(
        InvoiceFormat format,
        InvoiceDocument invoice,
        PdfRenderOptions? options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/validate/{format}</c> with the supplied XML uploaded as
    /// <c>multipart/form-data</c>. Returns the parsed validation result.
    /// </summary>
    Task<ValidationResult> ValidateXmlAsync(
        XmlInvoiceFormat format,
        string xml,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/validate/{format}</c> with the supplied PDF uploaded as
    /// <c>multipart/form-data</c>. Returns the parsed validation result.
    /// </summary>
    Task<ValidationResult> ValidatePdfAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/render/{format}/to/pdf</c> with the supplied XML uploaded
    /// as <c>multipart/form-data</c>. Returns the rendered visual PDF preview.
    /// </summary>
    Task<DocumentArtifact> RenderToPdfAsync(
        XmlInvoiceFormat format,
        string xml,
        PdfLanguage language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/extract/{target}</c> with the supplied PDF uploaded as
    /// <c>multipart/form-data</c>. Returns either the structured invoice document
    /// (JSON) or the embedded EN 16931 CII XML, per <paramref name="target"/>.
    /// </summary>
    Task<DocumentArtifact> ExtractAsync(
        ExtractTarget target,
        byte[] pdf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/embed/{format}</c> with the supplied PDF and CII XML
    /// uploaded as <c>multipart/form-data</c>. Returns a hybrid PDF/A-3 with the
    /// XML embedded (Factur-X or ZUGFeRD).
    /// </summary>
    Task<DocumentArtifact> EmbedAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        string ciiXml,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/convert/{source}/to/{target}</c> with the supplied document
    /// uploaded as <c>multipart/form-data</c>. The source bytes are XML for plain-XML
    /// sources and a hybrid PDF for Factur-X / ZUGFeRD sources; the result is XML or
    /// a hybrid PDF depending on <paramref name="target"/>.
    /// </summary>
    Task<DocumentArtifact> ConvertAsync(
        InvoiceFormat source,
        InvoiceFormat target,
        byte[] content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default);
}
