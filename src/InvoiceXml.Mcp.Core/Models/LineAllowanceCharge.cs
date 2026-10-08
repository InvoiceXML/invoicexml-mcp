using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// An invoice line allowance (BG-27, BT-136..BT-140) or charge (BG-28,
/// BT-141..BT-145). The API's <c>LineAllowance</c> and <c>LineCharge</c>
/// share this exact wire shape. Line allowances and charges carry no VAT
/// fields: they take the line's VAT category.
/// </summary>
public sealed class LineAllowanceCharge
{
    [Description("Amount excluding VAT (BT-136 allowance, BT-141 charge). Required. The line net amount (BT-131) must already subtract line allowances and add line charges.")]
    public decimal? Amount { get; set; }

    [Description("Base amount the percentage applies to (BT-137 / BT-142). Optional.")]
    public decimal? BaseAmount { get; set; }

    [Description("Percentage applied to the base amount, e.g. 10 for 10% (BT-138 / BT-143). Optional.")]
    public decimal? Percentage { get; set; }

    [Description("Reason in words (BT-139 / BT-144). Supply a reason or a reason code.")]
    public string? Reason { get; set; }

    [Description("Reason code (BT-140 / BT-145): UNTDID 5189 for allowances, UNTDID 7161 for charges.")]
    public string? ReasonCode { get; set; }

    /// <summary>Additional fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
