using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 seller (BG-4) or buyer (BG-7) party. The API has separate
/// <c>SellerParty</c> and <c>BuyerParty</c> classes, but their JSON shapes
/// overlap on the fields the LLM is most likely to fill. A single
/// <see cref="Party"/> covers both; any seller-only or buyer-only properties
/// (Contact, Identifiers, LegalRegistration, ElectronicAddress, etc.) flow
/// through via <see cref="Additional"/>.
/// </summary>
public sealed class Party
{
    [Required(ErrorMessage = "Party name is required (BT-27 for seller, BT-44 for buyer).")]
    [Description("Trading / commercial name of the party. Required by EN 16931.")]
    public string? Name { get; set; }

    [Description("Trading or doing-business-as name, if different from the legal name (BT-28 seller, BT-45 buyer).")]
    public string? TradingName { get; set; }

    [Required(ErrorMessage = "Party postal address is required (BG-5 for seller, BG-8 for buyer).")]
    [Description("Postal address of the party. Required by EN 16931.")]
    public PostalAddress? PostalAddress { get; set; }

    [Description("VAT identifier including country prefix (e.g. 'DE123456789'). BT-31 for seller, BT-48 for buyer.")]
    public string? VatIdentifier { get; set; }

    /// <summary>Additional party fields (Contact, Identifiers, LegalRegistration, ElectronicAddress, ...) flow through unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
