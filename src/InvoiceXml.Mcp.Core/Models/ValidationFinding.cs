using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// One error or warning produced by the validation pipeline. Shape mirrors the
/// objects emitted by the API's <c>ValidationFindingFormatter</c>; unmodelled
/// fields flow through via <see cref="Additional"/>.
/// </summary>
public sealed class ValidationFinding
{
    /// <summary>Identifier of the violated rule (e.g. <c>BR-CO-15</c>, <c>PDF-EMBED</c>).</summary>
    public string? Rule { get; init; }

    /// <summary>Line number in the source XML, when known.</summary>
    public int? Line { get; init; }

    /// <summary>Human-readable explanation, friendly when the rule has a known mapping.</summary>
    public string? Message { get; init; }

    /// <summary>EN 16931 Business Term codes touched by this finding.</summary>
    public IReadOnlyList<string>? BtCodes { get; init; }

    /// <summary>Logical field paths touched by this finding.</summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>The original validator output line (Schematron / XSD message).</summary>
    public string? Raw { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; init; }
}
