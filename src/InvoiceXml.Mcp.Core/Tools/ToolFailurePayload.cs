using System.Text.Json;
using InvoiceXml.Mcp.Core.Models;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// JSON shape the calling LLM sees inside an artifact-producing tool's failure
/// result (a <see cref="ModelContextProtocol.Protocol.CallToolResult"/> with
/// <see cref="ModelContextProtocol.Protocol.CallToolResult.IsError"/> set to
/// <see langword="true"/>). Shared by <c>create_invoice</c>, <c>render_invoice</c>,
/// <c>extract_invoice</c>, <c>embed_invoice</c> and <c>convert_invoice</c> so the
/// failure contract never drifts between tools.
/// </summary>
/// <remarks>
/// <para>
/// Success paths are not modelled by a POCO — content goes straight into
/// <see cref="ModelContextProtocol.Protocol.CallToolResult.Content"/> via
/// <see cref="ToolResults.ForArtifact"/>. This type exists only to give the
/// failure JSON a stable, testable contract.
/// </para>
/// <para>
/// The agent framework reads <c>failureCategory</c> + <c>guidance</c> to decide
/// whether retrying with corrections is worthwhile, and walks <c>errors</c> when
/// constructing a corrected request. A <see langword="null"/> <c>failureCategory</c>
/// denotes a client-side input rejection (the request never reached the API).
/// </para>
/// </remarks>
public sealed class ToolFailurePayload
{
    /// <summary>Always <see langword="false"/> for instances of this type.</summary>
    public required bool Success { get; init; }

    /// <summary>One-line human-readable description of what went wrong.</summary>
    public required string Summary { get; init; }

    /// <summary>The format slug(s) involved (e.g. <c>"facturx"</c>, or <c>"ubl-&gt;facturx"</c> for a conversion).</summary>
    public string? Format { get; init; }

    /// <summary>
    /// Classification of the failure. The LLM should use this (combined with
    /// <see cref="Guidance"/>) to decide whether a retry can help.
    /// <see langword="null"/> for client-side input errors that never hit the API.
    /// </summary>
    public ToolFailureCategory? FailureCategory { get; init; }

    /// <summary>HTTP status code returned by the API. <see langword="null"/> for network-layer or input failures.</summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Per-error breakdown. For API validation failures each entry carries a
    /// <c>message</c>, the EN 16931 <c>btCodes</c> involved, and the JSON
    /// <c>fields</c> path that was rejected. For input failures a single entry
    /// carries an <c>INPUT-…</c> <c>rule</c>.
    /// </summary>
    public IReadOnlyList<ValidationFinding>? Errors { get; init; }

    /// <summary>Plain-language hint for the LLM about whether and how to retry.</summary>
    public string? Guidance { get; init; }

    /// <summary>
    /// For a <see cref="ToolFailureCategory.Validation"/> failure, the invoice document the
    /// API extracted/parsed, echoed back as a BT-first EN 16931 JSON object. Present so the
    /// caller can correct the fields named in <see cref="Errors"/> on THIS document and call
    /// <c>create_invoice</c> with it, instead of re-running an extraction-based tool (which
    /// would reproduce the same errors). <see langword="null"/> when the API did not return one.
    /// </summary>
    public JsonElement? InvoiceData { get; init; }
}
