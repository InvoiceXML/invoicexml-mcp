using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 payment instructions (BG-16) plus the payment terms (BT-20).
/// Mirrors the API's <c>PaymentDetails</c>.
/// </summary>
public sealed class PaymentDetails
{
    [Description(
        "UNTDID 4461 payment means code (BT-81), e.g. '30' (credit transfer), '58' (SEPA credit transfer), " +
        "'59' (SEPA direct debit), '48' (bank card), '10' (cash). Defaults to '30' server-side. XRechnung requires " +
        "payment instructions to be present.")]
    public string? PaymentMeansCode { get; set; }

    [Description("Payment means in words (BT-82), e.g. 'Bank transfer'. Optional.")]
    public string? PaymentMeansText { get; set; }

    [Description("Remittance information (BT-83): the reference the payer should quote, usually the invoice number.")]
    public string? RemittanceInformation { get; set; }

    [Description("Payee account identifier (BT-84): the IBAN for credit transfers, or a proprietary account number.")]
    public string? PaymentAccountIdentifier { get; set; }

    [Description("Payee account name (BT-85).")]
    public string? PaymentAccountName { get; set; }

    [Description("BIC of the payee's bank (BT-86). Optional.")]
    public string? Bic { get; set; }

    [Description("SEPA direct debit mandate reference (BT-89). Direct debit (code '59') only.")]
    public string? MandateReference { get; set; }

    [Description(
        "Payment terms in words (BT-20), e.g. 'Payable within 30 days'. When omitted the server writes a " +
        "default sentence in the invoice language (options.language).")]
    public string? PaymentTerms { get; set; }

    /// <summary>Additional payment fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
