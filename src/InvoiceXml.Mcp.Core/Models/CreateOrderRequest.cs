namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Envelope matching the API's <c>CreateOrderRequest</c>: orders reuse the
/// invoice document model under an <c>order</c> key. Used by the typed HTTP
/// client; tool callers don't see this type directly.
/// </summary>
internal sealed class CreateOrderRequest
{
    public required InvoiceDocument Order { get; init; }
    public CreateOrderOptions Options { get; init; } = new();
}
