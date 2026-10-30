using System.ComponentModel;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// The email that carries the invoice to its recipient. Mirrors the API's
/// <c>EmailMessage</c>. The invoice file itself is attached by the API.
/// </summary>
public sealed class EmailMessage
{
    [Description("The sender address, for example invoices@yourcompany.com. Any address on one of the account's verified sending domains.")]
    public required string From { get; init; }

    [Description("Optional display name of the sender, for example 'Your Company Billing'. At most 100 characters, no line breaks.")]
    public string? FromName { get; init; }

    [Description("The recipient's email address. One address.")]
    public required string To { get; init; }

    [Description("Optional addresses that receive a visible copy, for example an accounting inbox. cc and bcc hold at most 2 addresses together.")]
    public List<string>? Cc { get; init; }

    [Description("Optional addresses that receive a hidden copy. cc and bcc hold at most 2 addresses together.")]
    public List<string>? Bcc { get; init; }

    [Description("Optional address the recipient's replies go to, when it differs from the sender. Any domain.")]
    public string? ReplyTo { get; init; }

    [Description("Optional subject, at most 200 characters, no line breaks. When omitted the subject is the document type and its number, for example 'Invoice 2026-0042'.")]
    public string? Subject { get; init; }

    [Description("Optional plain-text message, at most 10,000 characters, sent exactly as given in any language. When omitted a short English text names the attached invoice or credit note and its number.")]
    public string? Message { get; init; }
}
