using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 delivery information (BG-13). Mirrors the API's
/// <c>DeliveryInformation</c>.
/// </summary>
public sealed class DeliveryInformation
{
    [Description("Name of the party the goods or services were delivered to (BT-70).")]
    public string? ReceiverName { get; set; }

    [Description("Identifier of the delivery location (BT-71), e.g. a GLN or a French SIRET.")]
    public string? LocationIdentifier { get; set; }

    [Description("ISO 6523 ICD scheme of the location identifier (BT-71-1), e.g. '0088' (GLN) or '0009' (SIRET).")]
    public string? LocationIdentifierSchemeId { get; set; }

    [Description("Actual delivery date (BT-72) in ISO 8601 (yyyy-MM-dd).")]
    public DateOnly? ActualDeliveryDate { get; set; }

    [Description("Deliver-to address (BG-15), when it differs from the buyer's address.")]
    public DeliveryAddress? DeliveryAddress { get; set; }

    /// <summary>Additional delivery fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
