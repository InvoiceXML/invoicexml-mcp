using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// Footer credit printed on PDFs the API renders (invoice templates and
/// validation reports). Wire values match the API's <c>footerBrand</c> field.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FooterBrand>))]
public enum FooterBrand
{
    /// <summary>The default "InvoiceXML" footer credit.</summary>
    [JsonStringEnumMemberName("invoicexml")] InvoiceXml,

    /// <summary>No footer credit (white label).</summary>
    [JsonStringEnumMemberName("none")] None,
}
