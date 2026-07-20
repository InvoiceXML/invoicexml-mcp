using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// What the <c>/v1/extract</c> family should pull out of an uploaded PDF.
/// Wire / URL values are lower-case slugs.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExtractTarget>))]
public enum ExtractTarget
{
    /// <summary>
    /// Structured invoice document as JSON (<c>POST /v1/extract/json</c>).
    /// The response is an envelope with the document under an <c>invoice</c> key.
    /// </summary>
    [JsonStringEnumMemberName("json")]
    Json,

    /// <summary>The embedded EN 16931 CII XML (<c>POST /v1/extract/xml</c>).</summary>
    [JsonStringEnumMemberName("xml")]
    Xml,

    /// <summary>
    /// Every embedded supporting document (BG-24 attachments with a BT-125
    /// payload) bundled as a ZIP archive (<c>POST /v1/extract/attachments</c>).
    /// </summary>
    [JsonStringEnumMemberName("attachments")]
    Attachments,
}
