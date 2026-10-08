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

    [Description("Seller's article number for the item (BT-155). Optional.")]
    public string? SellerIdentifier { get; set; }

    [Description("Buyer's article number for the item (BT-156). Optional.")]
    public string? BuyerIdentifier { get; set; }

    [Description("Standard item identifier (BT-157), e.g. a GTIN with ISO 6523 scheme '0160' (BT-157-1).")]
    public SchemeIdentifier? StandardIdentifier { get; set; }

    [Description("Item classifications (BT-158), e.g. CPV or UNSPSC codes. Optional.")]
    public List<ItemClassification>? Classifications { get; set; }

    [Description("Item attributes (BG-32): name / value pairs such as colour or serial number. Optional.")]
    public List<ItemProperty>? Attributes { get; set; }

    [Description("ISO 3166-1 alpha-2 country of origin of the item (BT-159). Optional.")]
    public string? CountryOfOrigin { get; set; }

    /// <summary>Item fields not modelled above flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
