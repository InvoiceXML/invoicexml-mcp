using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// UNTDID 1001 subset used by EN 16931 for invoices and credit notes.
/// Sent on the wire as the C# member name ("Invoice", "CreditNote", ...); the
/// API also accepts the numeric UNTDID code ("380", "381", ...) for callers
/// who prefer the raw form.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InvoiceTypeCode>))]
public enum InvoiceTypeCode
{
    /// <summary>Commercial invoice (UNTDID 380). The default.</summary>
    Invoice = 380,

    /// <summary>Credit note (UNTDID 381).</summary>
    CreditNote = 381,

    /// <summary>Debit note (UNTDID 383).</summary>
    DebitNote = 383,

    /// <summary>Corrected invoice (UNTDID 384).</summary>
    CorrectedInvoice = 384,

    /// <summary>Prepayment invoice (UNTDID 386).</summary>
    PrepaymentInvoice = 386,

    /// <summary>Self-billed invoice (UNTDID 389).</summary>
    SelfBilledInvoice = 389,
}
