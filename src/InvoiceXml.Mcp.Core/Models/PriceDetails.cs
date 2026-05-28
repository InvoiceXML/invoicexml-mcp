using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 line price details group (BG-29). Mirrors the API's <c>PriceDetails</c>.
/// </summary>
public sealed class PriceDetails
{
    [Required(ErrorMessage = "Item net price (BT-146) is required.")]
    [Description("Net price of the item, excluding VAT (BT-146). Required.")]
    public decimal? NetPrice { get; set; }

    [Description("Discount applied to the gross price to arrive at the net price (BT-147).")]
    public decimal? DiscountAmount { get; set; }

    [Description("Gross price of the item before discount (BT-148).")]
    public decimal? GrossPrice { get; set; }

    [Description("Base quantity to which the net price applies (BT-149). Defaults to 1.")]
    public decimal? PriceBaseQuantity { get; set; }

    [Description("Unit of measure of the price base quantity (BT-150). UN/ECE Rec 20 code, e.g. 'EA', 'HUR'.")]
    public string? PriceBaseUnit { get; set; }

    /// <summary>Additional price fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
