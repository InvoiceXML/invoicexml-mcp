using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Seller contact (BG-6) or buyer contact (BG-9). The API's
/// <c>SellerContact</c> and <c>BuyerContact</c> share this exact wire shape.
/// </summary>
public sealed class PartyContact
{
    [Description("Contact point: a person or department name (BT-41 seller, BT-56 buyer).")]
    public string? Name { get; set; }

    [Description("Contact telephone number (BT-42 seller, BT-57 buyer).")]
    public string? Phone { get; set; }

    [Description("Contact email address (BT-43 seller, BT-58 buyer).")]
    public string? Email { get; set; }

    /// <summary>Additional contact fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
