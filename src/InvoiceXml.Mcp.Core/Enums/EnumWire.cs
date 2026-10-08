using System.Text.Json;
using InvoiceXml.Mcp.Core.Services;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// The single place an enum turns into its API spelling: the
/// <c>JsonStringEnumMemberName</c> value, so route slugs, form fields, JSON
/// bodies and tool-result labels share one source of truth (for example
/// <c>order-x</c>, <c>br-fr</c>, <c>none</c>).
/// </summary>
internal static class EnumWire
{
    /// <summary>Returns the wire value of <paramref name="value"/> without the JSON quotes.</summary>
    public static string Slug<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonSerializer.Serialize(value, InvoiceXmlJsonOptions.Default).Trim('"');
}
