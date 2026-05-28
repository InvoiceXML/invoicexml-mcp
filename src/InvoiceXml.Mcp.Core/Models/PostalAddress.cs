using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 postal address. Shape matches the API's <c>SellerPostalAddress</c>
/// (BG-5) and <c>BuyerPostalAddress</c> (BG-8) which carry identical fields on
/// the wire. The only mandatory member is <see cref="Country"/>; the LLM is
/// free to fill or omit anything else.
/// </summary>
public sealed class PostalAddress
{
    [Description("Street name and number, first line. Optional but recommended.")]
    public string? Line1 { get; set; }

    [Description("Additional street line (suite, floor, building).")]
    public string? Line2 { get; set; }

    [Description("Third street line (rarely used).")]
    public string? Line3 { get; set; }

    [Description("City name.")]
    public string? City { get; set; }

    [Description("Postal / ZIP code.")]
    public string? PostCode { get; set; }

    [Description("Country subdivision (region / state / province).")]
    public string? CountrySubdivision { get; set; }

    [Required(ErrorMessage = "Country code is required (BT-40 for seller, BT-55 for buyer).")]
    [Description("ISO 3166-1 alpha-2 country code (e.g. 'DE', 'FR', 'US'). Required by EN 16931.")]
    public string? Country { get; set; }

    /// <summary>Additional address fields that flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
