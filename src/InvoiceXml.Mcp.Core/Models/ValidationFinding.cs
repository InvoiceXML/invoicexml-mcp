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

    /// <summary>
    /// Validation layer that produced the finding: <c>xsd</c> (structure),
    /// <c>en16931</c> (European standard base rules), or <c>cius</c> (the
    /// profile overlay: Peppol BIS, XRechnung, NLCIUS, PINT or the
    /// Factur-X/ZUGFeRD profile rules).
    /// </summary>
    public string? Layer { get; init; }

    /// <summary>
    /// 1-based invoice line item number the finding relates to, or
    /// <see langword="null"/> for document-level findings. Not repeated inside
    /// <see cref="Message"/>: compose any "Line 2:" display prefix from this field.
    /// </summary>
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
