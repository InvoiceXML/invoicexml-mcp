using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Structured shape of the InvoiceXML API's error responses. Follows RFC 7807
/// (<c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>) with the API's own
/// extensions (<c>valid</c>, <c>errorCode</c>, <c>errors</c>). Returned by
/// <see cref="Services.InvoiceXmlApiException.TryParseProblem"/> so tools can
/// turn a 4xx response into actionable feedback for the calling LLM.
/// </summary>
public sealed class InvoiceXmlApiProblem
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public int? Status { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("valid")]
    public bool? Valid { get; init; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<ValidationFinding>? Errors { get; init; }

    /// <summary>Catch-all for additional fields the API may add over time.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; init; }
}
