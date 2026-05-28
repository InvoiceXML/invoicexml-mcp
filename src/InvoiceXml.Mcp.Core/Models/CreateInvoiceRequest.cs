namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Envelope matching the API's <c>CreateInvoiceRequest</c>. Used by the typed
/// HTTP client; tool callers don't see this type directly.
/// </summary>
internal sealed class CreateInvoiceRequest
{
    public required InvoiceDocument Invoice { get; init; }
    public PdfRenderOptions Options { get; init; } = new();
}
