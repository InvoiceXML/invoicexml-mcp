using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Optional creation settings sent as the request's <c>options</c> object:
/// the visual render settings for the Factur-X / ZUGFeRD hybrid PDF face,
/// plus format-specific switches (UBL CIUS profile, XRechnung version,
/// custom PDF visual layer). Every field is optional; the API applies
/// sensible defaults when omitted.
/// </summary>
public sealed class CreateInvoiceOptions
{
    [Description("Language of the human-readable PDF face. Defaults to English. Hybrid PDF formats (facturx, zugferd) only.")]
    public PdfLanguage? Language { get; set; }

    [Description("CSS-style hex colour applied to headings and accent rules (e.g. '#1F4E79'). Hybrid PDF formats only. Optional.")]
    public string? BrandColor { get; set; }

    [Description(
        "UBL CIUS profile, format 'ubl' only. Selects both the CustomizationID stamped into the XML and the " +
        "validation rule set applied before delivery. One of: 'peppol-bis-3' (default, Peppol network ready), " +
        "'en16931' (plain EN 16931, use when the invoice does not travel over Peppol), 'nlcius' (Netherlands), " +
        "'ehf' (Norway), 'xrechnung' (German B2G in UBL syntax), 'pint' (Peppol International).")]
    public string? Profile { get; set; }

    [Description(
        "Specification version to produce and validate against, format 'xrechnung' only. " +
        "Allowed value: '3.0' (the version currently in force). Omit to always use the currently effective version.")]
    public string? Version { get; set; }

    [Description(
        "Public https:// URL of a PDF to use as the visual layer of the hybrid invoice, formats 'facturx' and " +
        "'zugferd' only. When set, the API downloads it and embeds the generated XML into it instead of rendering " +
        "the built-in template; 'language' and 'brandColor' then have no effect. Max 20 MB.")]
    public string? PdfUrl { get; set; }
}
