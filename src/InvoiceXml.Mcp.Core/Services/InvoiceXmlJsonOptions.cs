using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Services;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> tuned to the InvoiceXML API's wire
/// shape: camelCase property names, enums as strings (with <c>JsonStringEnumMemberName</c>
/// respected), tolerant nullable handling. Used for both request bodies and
/// response parsing.
/// </summary>
internal static class InvoiceXmlJsonOptions
{
    /// <summary>Shared read-only options instance.</summary>
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
