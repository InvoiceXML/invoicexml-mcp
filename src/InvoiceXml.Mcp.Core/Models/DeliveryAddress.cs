using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 deliver-to address (BG-15). Same fields as <see cref="PostalAddress"/>,
/// but every member is optional, matching the API's <c>DeliveryPostalAddress</c>.
/// </summary>
public sealed class DeliveryAddress
{
    [Description("Street name and number, first line (BT-75).")]
    public string? Line1 { get; set; }

    [Description("Additional street line (BT-76).")]
    public string? Line2 { get; set; }

    [Description("Third street line (BT-165).")]
    public string? Line3 { get; set; }

    [Description("City name (BT-77).")]
    public string? City { get; set; }

    [Description("Postal / ZIP code (BT-78).")]
    public string? PostCode { get; set; }

    [Description("Country subdivision (region / state / province) (BT-79).")]
    public string? CountrySubdivision { get; set; }

    [Description("ISO 3166-1 alpha-2 country code (BT-80), e.g. 'DE', 'FR'.")]
    public string? Country { get; set; }

    /// <summary>Additional address fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
