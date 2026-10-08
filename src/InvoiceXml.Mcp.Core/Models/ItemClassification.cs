using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 item classification (BT-158). Mirrors the API's
/// <c>ItemClassification</c> record.
/// </summary>
public sealed class ItemClassification
{
    [Required(ErrorMessage = "Item classification identifier (BT-158) is required.")]
    [Description("Classification code (BT-158), e.g. a CPV or UNSPSC code. Required.")]
    public string? Identifier { get; set; }

    [Description("UNTDID 7143 scheme of the code (BT-158-1), e.g. 'STI' (CPV), 'TST' (UNSPSC), 'HS' (customs tariff).")]
    public string? SchemeId { get; set; }

    [Description("Version of the classification scheme (BT-158-2). Optional.")]
    public string? SchemeVersion { get; set; }

    /// <summary>Additional fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
