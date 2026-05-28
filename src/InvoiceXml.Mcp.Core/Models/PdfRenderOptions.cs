using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Visual render settings for the Factur-X / ZUGFeRD hybrid PDF face.
/// Ignored when the requested format produces plain XML.
/// </summary>
public sealed class PdfRenderOptions
{
    [Description("Language of the human-readable PDF face. Defaults to English.")]
    public PdfLanguage? Language { get; set; }

    [Description("CSS-style hex colour applied to headings and accent rules (e.g. '#1F4E79'). Optional.")]
    public string? BrandColor { get; set; }
}
