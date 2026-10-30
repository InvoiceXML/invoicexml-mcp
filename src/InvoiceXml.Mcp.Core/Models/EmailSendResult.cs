namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Answer of the API's email routes. Mirrors the API's <c>EmailSendResponse</c>.
/// </summary>
public sealed class EmailSendResult
{
    /// <summary>Always <c>accepted</c>: handed over for delivery, not yet received by the recipient's server.</summary>
    public required string Status { get; init; }

    /// <summary>Identifier of the sent email, to ask about a delivery later.</summary>
    public required string MessageId { get; init; }
}
