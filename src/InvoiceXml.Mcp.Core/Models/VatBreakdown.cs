using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 VAT breakdown (BG-23). One entry per distinct (category, rate) pair
/// across the invoice's lines and document-level allowances/charges.
/// </summary>
/// <remarks>
/// The API doesn't apply <c>[Required]</c> at the C# level on any of these —
/// cardinality is enforced downstream by XSD / Schematron — so we don't either,
/// to stay permissive and let the LLM build partial breakdowns the API can
/// reject with a precise error.
/// </remarks>
public sealed class VatBreakdown
{
    [Description("VAT category code for this bucket (BT-118). Required by the standard.")]
    public VatCategoryCode? CategoryCode { get; set; }

    [Description("Taxable amount for this bucket (BT-116). Required by the standard.")]
    public decimal? TaxableAmount { get; set; }

    [Description("VAT amount for this bucket (BT-117). Required by the standard.")]
    public decimal? TaxAmount { get; set; }

    [Description("VAT rate applied, as a percentage (e.g. 19 for 19%). BT-119. Required for standard-rated categories.")]
    public decimal? Rate { get; set; }

    [Description("Reason text required when CategoryCode is E, AE, K, G, O, L or M (BT-120).")]
    public string? ExemptionReasonText { get; set; }

    [Description("Optional exemption reason code (BT-121).")]
    public string? ExemptionReasonCode { get; set; }

    /// <summary>Additional VAT breakdown fields that flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
