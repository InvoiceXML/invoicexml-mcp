using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Optional settings sent as the <c>options</c> object of the Order-X create
/// endpoints. Mirrors the API's <c>OrderOptions</c>; every field is optional.
/// </summary>
public sealed class CreateOrderOptions
{
    [Description("Order-X conformance profile: 'basic', 'comfort' (default) or 'extended'.")]
    public OrderProfile? Profile { get; set; }

    [Description("Order document type (UNTDID 1001): '220' purchase order (default), '230' order change, '231' order response.")]
    public OrderTypeCode? TypeCode { get; set; }

    [Description("Language of the rendered PDF face: EN (default), DE or FR. Format 'order-x' only.")]
    public PdfLanguage? Language { get; set; }

    [Description("Hex colour '#RRGGBB' applied to headings and accent rules. Format 'order-x' only.")]
    public string? BrandColor { get; set; }

    [Description(
        "Public https:// URL of a PDF to use as the visual layer instead of the built-in order template. " +
        "Format 'order-x' only; 'language', 'brandColor', 'logo', 'logoUrl' and 'footerBrand' then have no effect.")]
    public string? PdfUrl { get; set; }

    [Description(
        "Public https:// URL of the buyer's logo (PNG or JPEG, max 2 MB and 4 megapixels), printed top-left on the " +
        "rendered order. To send the image itself instead, use 'logo'; set one of the two, not both. " +
        "Format 'order-x' only; ignored when 'pdfUrl' is set.")]
    public string? LogoUrl { get; set; }

    [Description(
        "The buyer's logo as the PNG or JPEG file itself, in plain base64 without a data: prefix (max 2 MB " +
        "decoded and 4 megapixels), printed top-left on the rendered order exactly as 'logoUrl' would be. " +
        "Use it for a logo that is not at a public URL. Set 'logo' or 'logoUrl', not both. Format 'order-x' " +
        "only; ignored when 'pdfUrl' is set.")]
    public string? Logo { get; set; }

    [Description("Footer credit on the rendered PDF face: 'invoicexml' (default) or 'none' to omit it. Format 'order-x' only.")]
    public FooterBrand? FooterBrand { get; set; }
}
