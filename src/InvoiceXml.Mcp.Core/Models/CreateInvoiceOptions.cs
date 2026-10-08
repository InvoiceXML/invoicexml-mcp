using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Optional creation settings sent as the request's <c>options</c> object:
/// the invoice language, the visual render settings for the Factur-X /
/// ZUGFeRD hybrid PDF face, plus format-specific switches (UBL CIUS profile,
/// extra national rules, custom PDF visual layer). Every field is optional;
/// the API applies sensible defaults when omitted.
/// </summary>
public sealed class CreateInvoiceOptions
{
    [Description(
        "Language of the invoice: EN (default), DE or FR. Applies to every format: it words the texts the server " +
        "adds when the document leaves them empty (the payment terms BT-20 and the VAT exemption reason BT-120). " +
        "On the hybrid PDF formats (facturx, zugferd) it also localises the labels and dates of the PDF face. " +
        "Text you supply always wins.")]
    public PdfLanguage? Language { get; set; }

    [Description("Hex colour '#RRGGBB' applied to headings and accent rules (e.g. '#1F4E79'). Hybrid PDF formats only. Optional.")]
    public string? BrandColor { get; set; }

    [Description(
        "UBL CIUS profile, format 'ubl' only. Selects both the CustomizationID stamped into the XML and the " +
        "validation rule set applied before delivery. One of: 'peppol-bis-3' (default, Peppol network ready), " +
        "'en16931' (plain EN 16931, use when the invoice does not travel over Peppol), 'nlcius' (Netherlands), " +
        "'ehf' (Norway), 'xrechnung' (German B2G in UBL syntax), 'pint' (Peppol International).")]
    public string? Profile { get; set; }

    [Description(
        "XML syntax to produce, format 'xrechnung' only. XRechnung is defined in two interchangeable syntaxes: " +
        "'ubl' (UBL 2.1, the default and what most German receivers expect) or 'cii' (UN/CEFACT Cross Industry " +
        "Invoice). The German rules (BR-DE) and the declared specification identifier are identical in both. " +
        "Other formats ignore it.")]
    public string? Syntax { get; set; }

    [Description(
        "Extra national rule sets to validate on top of the format's own rules, without changing the declared " +
        "specification identifier. Allowed value: 'br-fr' (the French e-invoicing mandate's BR-FR rules, applied " +
        "by the French platforms to every domestic invoice). Formats 'facturx', 'cii' and 'ubl' only; the other " +
        "formats reject it. Violations come back as ordinary validation errors with BR-FR-* rule ids. " +
        "Set it by default on format 'facturx' (almost every Factur-X invoice goes over the French network); " +
        "omit it only when the user explicitly says the invoice will not be sent over that network.")]
    public List<ExtraRuleset>? Rules { get; set; }

    [Description(
        "Public https:// URL of a PDF to use as the visual layer of the hybrid invoice, formats 'facturx' and " +
        "'zugferd' only. When set, the API downloads it and embeds the generated XML into it instead of rendering " +
        "the built-in template; 'language' then only affects the XML texts, and 'brandColor', 'logoUrl' and " +
        "'footerBrand' have no effect. Max 20 MB.")]
    public string? PdfUrl { get; set; }

    [Description(
        "Public https:// URL of the seller's logo (PNG or JPEG, max 2 MB and 4 megapixels), printed top-left on " +
        "the rendered PDF face. Formats 'facturx' and 'zugferd' with the built-in template only; ignored when " +
        "'pdfUrl' is set.")]
    public string? LogoUrl { get; set; }

    [Description(
        "When true, the rendered PDF face gains an 'Electronic invoice details' section listing the technical " +
        "properties that do not appear on a classic invoice layout: specification identifier (BT-24), business " +
        "process type, party identifiers, electronic addresses, supporting documents, and similar. Hybrid PDF " +
        "formats ('facturx', 'zugferd') with the built-in template only; has no effect when 'pdfUrl' supplies the " +
        "visual layer.")]
    public bool? IncludeAdvancedProperties { get; set; }

    [Description(
        "Footer credit on the rendered PDF face: 'invoicexml' (default) or 'none' to omit it. Formats 'facturx' " +
        "and 'zugferd' with the built-in template only; ignored when 'pdfUrl' is set.")]
    public FooterBrand? FooterBrand { get; set; }
}
