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
        CreateInvoiceOptions? options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/create/cio</c> (plain Cross-Industry Order XML) or
    /// <c>POST /v1/create/order-x</c> (hybrid PDF/A-3) with the supplied order.
    /// Orders reuse the invoice document model, sent under an <c>order</c> key.
    /// </summary>
    Task<DocumentArtifact> CreateOrderAsync(
        OrderFormat format,
        InvoiceDocument order,
        CreateOrderOptions? options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/validate/order-x</c> with the supplied document uploaded
    /// as <c>multipart/form-data</c>: a hybrid Order-X PDF or the raw CIO XML,
    /// told apart by <paramref name="contentType"/>. Returns the parsed validation result.
    /// <paramref name="validationLanguage"/> words the findings; omitted when null.
    /// </summary>
    Task<ValidationResult> ValidateOrderAsync(
        byte[] content,
        string contentType,
        string fileName,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>GET /v1/me</c>: the caller's plan and credit balance. The API does
    /// not charge a credit for this call.
    /// </summary>
    Task<AccountInfo> GetAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/validate/{format}</c> with the supplied XML uploaded as
    /// <c>multipart/form-data</c>. Returns the parsed validation result.
    /// <paramref name="rules"/> adds extra national rule sets (one <c>rules</c>
    /// form part each); the API accepts them on <c>ubl</c> and <c>cii</c>.
    /// <paramref name="validationLanguage"/> words the findings; omitted when null.
    /// </summary>
    Task<ValidationResult> ValidateXmlAsync(
        XmlInvoiceFormat format,
        string xml,
        IReadOnlyList<ExtraRuleset>? rules,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/validate/{format}</c> with the supplied PDF uploaded as
    /// <c>multipart/form-data</c>. Returns the parsed validation result.
    /// <paramref name="rules"/> adds extra national rule sets; the API accepts
    /// them on <c>facturx</c> only. <paramref name="validationLanguage"/> words the
    /// findings; omitted when null.
    /// </summary>
    Task<ValidationResult> ValidatePdfAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        IReadOnlyList<ExtraRuleset>? rules,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/validate/{format}/report</c> with the supplied invoice
    /// uploaded as <c>multipart/form-data</c>. Runs the exact same validation
    /// pipeline as the JSON endpoints but returns a printable PDF compliance
    /// report; the verdict travels in the <c>X-Invoice-Valid</c> response header.
    /// The content bytes are XML for plain-XML formats (ubl/cii/xrechnung), a
    /// hybrid PDF for Factur-X / ZUGFeRD, and either for Order-X (the API takes
    /// a hybrid Order-X PDF or the raw CIO XML on one route). <paramref name="rules"/> adds extra
    /// national rule sets (ubl / cii / facturx); <paramref name="footerBrand"/>
    /// sets the report's footer credit, omitted when null.
    /// <paramref name="validationLanguage"/> words the whole report; omitted when null.
    /// </summary>
    Task<ValidationReportPdfResult> ValidationReportPdfAsync(
        ValidationReportFormat format,
        byte[] content,
        string contentType,
        string fileName,
        IReadOnlyList<ExtraRuleset>? rules,
        FooterBrand? footerBrand,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/render/{format}/to/pdf</c> with the supplied XML uploaded
    /// as <c>multipart/form-data</c>. Returns the rendered visual PDF preview.
    /// <paramref name="logoUrl"/> is fetched by the API, not by this client;
    /// <paramref name="logo"/> (the image itself as plain base64) travels as the
    /// <c>logo</c> file part; <paramref name="footerBrand"/> is omitted when null.
    /// </summary>
    Task<DocumentArtifact> RenderToPdfAsync(
        XmlInvoiceFormat format,
        string xml,
        PdfLanguage language,
        string? logoUrl,
        string? logo,
        FooterBrand? footerBrand,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/extract/{target}</c> with the supplied document uploaded
    /// as <c>multipart/form-data</c>. Returns the structured invoice document
    /// (JSON), the embedded EN 16931 CII XML, or the embedded supporting
    /// documents as a ZIP archive, per <paramref name="target"/>. Targets
    /// <c>json</c> and <c>attachments</c> take a hybrid PDF or a plain CII / UBL
    /// invoice XML (the API detects the type from <paramref name="contentType"/>
    /// and <paramref name="fileName"/>); target <c>xml</c> takes a hybrid PDF.
    /// </summary>
    Task<DocumentArtifact> ExtractAsync(
        ExtractTarget target,
        byte[] content,
        string contentType,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/embed/{format}</c> with the supplied PDF and CII XML
    /// uploaded as <c>multipart/form-data</c>. Returns a hybrid PDF/A-3 with the
    /// XML embedded (Factur-X or ZUGFeRD). <paramref name="rules"/> adds extra
    /// national rule sets to the pre-embed validation (facturx only).
    /// <paramref name="validationLanguage"/> words its findings; omitted when null.
    /// </summary>
    Task<DocumentArtifact> EmbedAsync(
        PdfInvoiceFormat format,
        byte[] pdf,
        string ciiXml,
        IReadOnlyList<ExtraRuleset>? rules,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/convert/{source}/to/{target}</c> with the supplied document
    /// uploaded as <c>multipart/form-data</c>. The source bytes are XML for plain-XML
    /// sources and a hybrid PDF for Factur-X / ZUGFeRD sources; the result is XML or
    /// a hybrid PDF depending on <paramref name="target"/>. <paramref name="language"/>
    /// and <paramref name="footerBrand"/> only travel on hybrid-PDF targets, where the
    /// API renders a PDF face; a null language is omitted so the API's default (English)
    /// applies. <paramref name="validationLanguage"/> words the findings of a failed
    /// conversion; omitted when null.
    /// </summary>
    Task<DocumentArtifact> ConvertAsync(
        InvoiceFormat source,
        InvoiceFormat target,
        byte[] content,
        string contentType,
        string fileName,
        PdfLanguage? language,
        FooterBrand? footerBrand,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls <c>POST /v1/transform/to/{target}</c> with the supplied PDF uploaded as
    /// <c>multipart/form-data</c>. Unlike <see cref="ConvertAsync"/> (a deterministic
    /// transcode of already-embedded XML), this runs AI data extraction over the PDF,
    /// so it also handles a plain (non-hybrid) PDF invoice, and rebuilds it in the
    /// target format. XML targets (ubl/cii/xrechnung) return XML; hybrid-PDF targets
    /// (facturx/zugferd) return a PDF/A-3. On the hybrid targets
    /// <paramref name="language"/> words the rendered pages and the XML's default texts
    /// (null sends <c>auto</c>: the language the invoice is written in, English when
    /// the renderer lacks it) and <paramref name="preserveOriginalPdf"/> keeps the
    /// uploaded PDF as the visual layer; <paramref name="buyerReference"/> supplies the
    /// BT-10 buyer reference required by XRechnung. <paramref name="validationLanguage"/>
    /// words the findings of a failed transform; omitted when null.
    /// </summary>
    Task<DocumentArtifact> TransformAsync(
        InvoiceFormat target,
        byte[] pdf,
        PdfLanguage? language,
        bool preserveOriginalPdf,
        string? buyerReference,
        ValidationLanguage? validationLanguage = null,
        CancellationToken cancellationToken = default);
}
