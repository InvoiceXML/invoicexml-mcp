using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 item attribute (BG-32). Mirrors the API's <c>ItemAttribute</c> record (renamed here because .NET reserves the Attribute suffix).
/// </summary>
public sealed class ItemProperty
{
    [Required(ErrorMessage = "Item attribute name (BT-160) is required.")]
    [Description("Attribute name (BT-160), e.g. 'Colour', 'Serial number'. Required.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Item attribute value (BT-161) is required.")]
    [Description("Attribute value (BT-161), e.g. 'Blue'. Required.")]
    public string? Value { get; set; }

    /// <summary>Additional fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
