using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 item information group (BG-31), the "what is being invoiced" block
/// of an invoice line. Mirrors the API's <c>ItemInformation</c>.
/// </summary>
public sealed class ItemInformation
{
    [Required(ErrorMessage = "Item name (BT-153) is required.")]
    [Description("Name of the item or service being invoiced. Required.")]
    public string? Name { get; set; }

    [Description("Longer description of the item (BT-154). Optional.")]
    public string? Description { get; set; }

    /// <summary>Additional item fields (SellerIdentifier, BuyerIdentifier, StandardIdentifier, Classifications, CountryOfOrigin) flow through.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
