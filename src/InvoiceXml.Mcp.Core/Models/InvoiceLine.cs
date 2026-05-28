using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 invoice line (BG-25). Mirrors the API's <c>InvoiceLine</c>: item
/// description, price details and VAT information each live in their own
/// nested group (BG-31 / BG-29 / BG-30 respectively), not as flat properties.
/// </summary>
public sealed class InvoiceLine
{
    [Description("Line identifier within the invoice (BT-126), e.g. '1', '2'. Required by EN 16931.")]
    public string? LineId { get; set; }

    [Description("Free-form note about this specific line (BT-127). Optional.")]
    public string? LineNote { get; set; }

    [Required(ErrorMessage = "Invoiced quantity (BT-129) is required.")]
    [Description("Quantity of the item being invoiced. Required.")]
    public decimal? Quantity { get; set; }

    [Description("UN/ECE Recommendation 20 unit code (BT-130), e.g. 'EA' (each), 'HUR' (hour), 'DAY' (day). Defaults to C62 (one) server-side if omitted.")]
    public string? UnitCode { get; set; }

    [Description("Line net amount (BT-131) = quantity * net unit price, excluding VAT. Server recomputes this if omitted.")]
    public decimal? LineNetAmount { get; set; }

    [Required(ErrorMessage = "Item information (BG-31) is required on every line.")]
    [Description("What's being invoiced: item name and description. Required.")]
    public ItemInformation? Item { get; set; }

    [Required(ErrorMessage = "Price details (BG-29) are required on every line.")]
    [Description("Unit pricing and discount. Required.")]
    public PriceDetails? PriceDetails { get; set; }

    [Required(ErrorMessage = "Line VAT information (BG-30) is required on every line.")]
    [Description("VAT category and rate for this line. Required.")]
    public LineVatInformation? VatInformation { get; set; }

    /// <summary>Additional line fields (ObjectIdentifier, period overrides, allowances/charges, ...) flow through unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
