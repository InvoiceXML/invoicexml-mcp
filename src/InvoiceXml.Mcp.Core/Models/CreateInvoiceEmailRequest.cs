namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Envelope matching the API's <c>CreateInvoiceEmailRequest</c>: the same
/// <c>invoice</c> and <c>options</c> as a create call, plus the <c>email</c>.
/// Used by the typed HTTP client; tool callers don't see this type directly.
/// </summary>
internal sealed class CreateInvoiceEmailRequest
{
    public required InvoiceDocument Invoice { get; init; }
    public CreateInvoiceOptions Options { get; init; } = new();
    public required EmailMessage Email { get; init; }
}
