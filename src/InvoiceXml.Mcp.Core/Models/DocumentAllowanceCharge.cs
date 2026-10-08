using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// A document level allowance (BG-20, BT-92..BT-98) or charge (BG-21,
/// BT-99..BT-105). The API's <c>DocumentLevelAllowance</c> and
/// <c>DocumentLevelCharge</c> share this exact wire shape.
/// </summary>
public sealed class DocumentAllowanceCharge
{
    [Description("Amount excluding VAT (BT-92 allowance, BT-99 charge). Required.")]
    public decimal? Amount { get; set; }

    [Description("Base amount the percentage applies to (BT-93 / BT-100). Optional.")]
    public decimal? BaseAmount { get; set; }

    [Description("Percentage applied to the base amount, e.g. 5 for 5% (BT-94 / BT-101). Optional.")]
    public decimal? Percentage { get; set; }

    [Description("VAT category of the allowance or charge (BT-95 / BT-102). Required. Must also appear in vatBreakdowns.")]
    public VatCategoryCode? VatCategoryCode { get; set; }

    [Description("VAT rate as a percentage (BT-96 / BT-103). Required for standard-rated categories.")]
    public decimal? VatRate { get; set; }

    [Description("Reason in words (BT-97 / BT-104), e.g. 'Early payment discount', 'Freight'. Supply a reason or a reason code.")]
    public string? Reason { get; set; }

    [Description("Reason code (BT-98 / BT-105): UNTDID 5189 for allowances (e.g. '95' discount), UNTDID 7161 for charges (e.g. 'FC' freight).")]
    public string? ReasonCode { get; set; }

    /// <summary>Additional fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
