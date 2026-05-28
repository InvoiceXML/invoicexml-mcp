using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Parsed response from a <c>/validate</c> endpoint.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>True when the invoice passed every layer of validation.</summary>
    public bool Valid { get; init; }

    /// <summary>Human-readable summary of the outcome.</summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Layer-by-layer flags and detected conformance level. Shape varies by format
    /// (e.g. PDF formats carry <c>hasEmbeddedXml</c>), so this is left open-ended.
    /// </summary>
    public ValidationData? Data { get; init; }

    /// <summary>Hard validation errors that caused the invoice to be rejected.</summary>
    public IReadOnlyList<ValidationFinding>? Errors { get; init; }

    /// <summary>Validation warnings — invoice passed, but these should be reviewed.</summary>
    public IReadOnlyList<ValidationFinding>? Warnings { get; init; }

    /// <summary>
    /// BT-coded summary rows of the validated invoice, when the parser could
    /// extract them. Opaque payload; consumers read it as JSON.
    /// </summary>
    public IReadOnlyList<JsonElement>? Report { get; init; }
}

/// <summary>
/// Variable-shape <c>data</c> payload accompanying a validation response. The
/// fields that exist depend on which validation pipeline ran.
/// </summary>
public sealed class ValidationData
{
    /// <summary>True when the XML matched its XSD.</summary>
    public bool? SchemaValid { get; init; }

    /// <summary>True when the XML passed the EN 16931 / CIUS Schematron layer.</summary>
    public bool? SchematronValid { get; init; }

    /// <summary>Name of the conformance profile detected (e.g. <c>EN16931</c>, <c>UBL 2.1</c>, <c>XRechnung</c>).</summary>
    public string? ConformanceLevel { get; init; }

    /// <summary>For PDF formats: whether an embedded XML was found in the PDF/A-3 attachment.</summary>
    public bool? HasEmbeddedXml { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; init; }
}
