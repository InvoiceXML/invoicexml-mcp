using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// Language the API words its validation findings in (and, on the report
/// endpoints, the whole report PDF). Wire values match the API's
/// <c>validationLanguage</c> field.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ValidationLanguage>))]
public enum ValidationLanguage
{
    /// <summary>English (the API default).</summary>
    [JsonStringEnumMemberName("en")] EN,

    /// <summary>German.</summary>
    [JsonStringEnumMemberName("de")] DE,

    /// <summary>French.</summary>
    [JsonStringEnumMemberName("fr")] FR,
}
