using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// Localisation of the human-readable PDF face of a hybrid Factur-X / ZUGFeRD invoice.
/// Member names match the wire values the API expects.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfLanguage>))]
public enum PdfLanguage
{
    /// <summary>English.</summary>
    EN,

    /// <summary>German.</summary>
    DE,

    /// <summary>French.</summary>
    FR,
}
