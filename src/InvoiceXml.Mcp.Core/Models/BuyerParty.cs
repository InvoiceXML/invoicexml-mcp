using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 buyer (BG-7). Mirrors the API's <c>BuyerParty</c>; property
/// names must match the API's camelCase wire shape.
/// </summary>
public sealed class BuyerParty
{
    [Required(ErrorMessage = "Buyer name (BT-44) is required.")]
    [Description("Buyer legal name (BT-44). Required.")]
    public string? Name { get; set; }

    [Description("Buyer trading or doing-business-as name, if different from the legal name (BT-45).")]
    public string? TradingName { get; set; }

    [Required(ErrorMessage = "Buyer postal address (BG-8) is required.")]
    [Description("Buyer postal address (BG-8). Required.")]
    public PostalAddress? PostalAddress { get; set; }

    [Description("Buyer contact (BG-9): contact point name, phone and email. Optional.")]
    public PartyContact? Contact { get; set; }

    [Description("Buyer identifiers (BT-46), each with an optional ISO 6523 scheme (BT-46-1), e.g. a GLN with scheme '0088'.")]
    public List<SchemeIdentifier>? Identifiers { get; set; }

    [Description(
        "Buyer legal registration identifier (BT-47) with optional ISO 6523 scheme (BT-47-1), e.g. a " +
        "company register number or a French SIREN with scheme '0002'.")]
    public SchemeIdentifier? LegalRegistration { get; set; }

    [Description("Buyer VAT identifier including the country prefix (BT-48), e.g. 'FR12345678901'.")]
    public string? VatIdentifier { get; set; }

    [Description(
        "Buyer electronic address (BT-49) with its Peppol EAS scheme (BT-49-1), e.g. a GLN with scheme " +
        "'0088', or in XRechnung the buyer's Leitweg-ID with scheme '0204'. Required by Peppol BIS (the " +
        "default for format 'ubl') and by XRechnung.")]
    public SchemeIdentifier? ElectronicAddress { get; set; }

    /// <summary>Buyer fields not modelled above flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
