using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// An identifier with its optional scheme qualifier. One shape covers every
/// identifier-plus-scheme pair of the API model: electronic addresses
/// (BT-34/BT-49), party identifiers (BT-29/BT-46), legal registrations
/// (BT-30/BT-47), the line object identifier (BT-128) and the item standard
/// identifier (BT-157). The API records all serialise as
/// <c>{ identifier, schemeId }</c>.
/// </summary>
public sealed class SchemeIdentifier
{
    [Required(ErrorMessage = "Identifier is required.")]
    [Description("The identifier value. Required.")]
    public string? Identifier { get; set; }

    [Description("Scheme of the identifier: a Peppol EAS code for electronic addresses (e.g. '9930', '0088'), an ISO 6523 ICD code otherwise (e.g. '0088' GLN, '0002' SIREN, '0009' SIRET).")]
    public string? SchemeId { get; set; }

    /// <summary>Additional fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
