using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 seller (BG-4). Mirrors the API's <c>SellerParty</c>; property
/// names must match the API's camelCase wire shape.
/// </summary>
public sealed class SellerParty
{
    [Required(ErrorMessage = "Seller name (BT-27) is required.")]
    [Description("Seller legal name (BT-27). Required.")]
    public string? Name { get; set; }

    [Description("Seller trading or doing-business-as name, if different from the legal name (BT-28).")]
    public string? TradingName { get; set; }

    [Required(ErrorMessage = "Seller postal address (BG-5) is required.")]
    [Description("Seller postal address (BG-5). Required.")]
    public PostalAddress? PostalAddress { get; set; }

    [Description(
        "Seller contact (BG-6): contact point name, phone and email. XRechnung requires all three " +
        "(BR-DE-2/5/6/7); otherwise optional.")]
    public PartyContact? Contact { get; set; }

    [Description(
        "Seller identifiers (BT-29), each with an optional ISO 6523 scheme (BT-29-1), e.g. a GLN with " +
        "scheme '0088'. Also carries the SEPA creditor identifier with scheme 'SEPA' for direct debit.")]
    public List<SchemeIdentifier>? Identifiers { get; set; }

    [Description(
        "Seller legal registration identifier (BT-30) with optional ISO 6523 scheme (BT-30-1), e.g. a " +
        "company register number or a French SIREN with scheme '0002'.")]
    public SchemeIdentifier? LegalRegistration { get; set; }

    [Description("Seller VAT identifier including the country prefix (BT-31), e.g. 'DE123456789'.")]
    public string? VatIdentifier { get; set; }

    [Description(
        "Seller national tax registration number (BT-32). Use it when the seller has no VAT identifier " +
        "(for example a small business exempt from VAT), or alongside BT-31 where local rules ask for both.")]
    public string? TaxRegistrationIdentifier { get; set; }

    [Description("Additional legal information about the seller (BT-33), e.g. share capital or registered office.")]
    public string? AdditionalLegalInformation { get; set; }

    [Description(
        "Seller electronic address (BT-34) with its Peppol EAS scheme (BT-34-1), e.g. identifier " +
        "'DE123456789' with scheme '9930', or a GLN with scheme '0088'. Required by Peppol BIS (the default " +
        "for format 'ubl') and by XRechnung.")]
    public SchemeIdentifier? ElectronicAddress { get; set; }

    /// <summary>Seller fields not modelled above flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
