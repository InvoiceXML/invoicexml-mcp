using System.Text.Json.Serialization;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Classification of why a tool call could not complete. Surfaced on the
/// failure result so the calling LLM (or its agent framework) can decide
/// whether a retry is worth attempting. Serialises as the member name
/// (<c>"Validation"</c>, <c>"Unauthorized"</c>, ...) rather than an integer
/// so the LLM gets readable JSON.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolFailureCategory>))]
public enum ToolFailureCategory
{
    /// <summary>The input failed API-side validation. Retry with corrected fields.</summary>
    Validation,

    /// <summary>The InvoiceXML API key is missing, invalid, or inactive. Retry won't help.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not allowed to use this endpoint. Retry won't help.</summary>
    Forbidden,

    /// <summary>Rate limit exceeded. Wait before retrying.</summary>
    RateLimited,

    /// <summary>InvoiceXML API returned a 5xx. May be transient — retry may help.</summary>
    Server,

    /// <summary>The MCP host could not reach the InvoiceXML API. Server configuration issue.</summary>
    Network,

    /// <summary>An unexpected client-side 4xx that isn't validation. Retry unlikely to help.</summary>
    Client,
}

/// <summary>
/// Translates exceptions thrown by <see cref="Interfaces.IInvoiceXmlClient"/>
/// into the structured failure shape consumed by every tool. Centralised so
/// the policy (which exceptions map to which category, what guidance the LLM
/// gets, etc.) lives in one place instead of three.
/// </summary>
internal static class ToolFailure
{
    public readonly record struct Translation(
        ToolFailureCategory Category,
        int? StatusCode,
        string Summary,
        string Guidance,
        IReadOnlyList<ValidationFinding>? Errors,
        InvoiceXmlApiProblem? Problem);

    public static Translation FromApiException(InvoiceXmlApiException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        var problem = ex.TryParseProblem();
        var status = (int)ex.StatusCode;
        var category = Classify(status);

        var summary = problem?.Detail
            ?? problem?.Title
            ?? $"InvoiceXML API returned {status} {ex.StatusCode}.";

        return new Translation(
            Category: category,
            StatusCode: status,
            Summary: summary,
            Guidance: GuidanceFor(category),
            Errors: problem?.Errors,
            Problem: problem);
    }

    public static Translation FromNetworkException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return new Translation(
            Category: ToolFailureCategory.Network,
            StatusCode: null,
            Summary: "Could not reach the InvoiceXML API.",
            Guidance: GuidanceFor(ToolFailureCategory.Network),
            Errors: null,
            Problem: null);
    }

    private static ToolFailureCategory Classify(int status) => status switch
    {
        400 => ToolFailureCategory.Validation,
        401 => ToolFailureCategory.Unauthorized,
        403 => ToolFailureCategory.Forbidden,
        429 => ToolFailureCategory.RateLimited,
        >= 500 and < 600 => ToolFailureCategory.Server,
        _ => ToolFailureCategory.Client,
    };

    private static string GuidanceFor(ToolFailureCategory category) => category switch
    {
        ToolFailureCategory.Validation =>
            "The invoice failed server-side validation. Each entry in 'errors' has " +
            "a 'message' (human-readable reason), 'btCodes' (EN 16931 Business Term ids) " +
            "and 'fields' (JSON paths into the request body). Fix the indicated fields " +
            "and call this tool again. Stop retrying if the same errors persist across attempts. " +
            "If the failure includes an 'invoiceData' object, it is the invoice the API already " +
            "parsed from this request (the extracted document for transform_invoice/convert_invoice, " +
            "or the document you sent to create_invoice). Prefer correcting the fields named in 'errors' " +
            "directly on that 'invoiceData' object and calling create_invoice with it (plus the originally " +
            "requested target format), rather than re-running an extraction-based tool that would only " +
            "reproduce the same errors.",

        ToolFailureCategory.Unauthorized =>
            "The MCP server's InvoiceXML credentials were rejected. This is a server " +
            "configuration issue, not something you can fix by changing the request. " +
            "Do not retry — surface the failure to the user.",

        ToolFailureCategory.Forbidden =>
            "The authenticated InvoiceXML account is not permitted to call this endpoint. " +
            "Do not retry — the user needs to upgrade their plan or contact support.",

        ToolFailureCategory.RateLimited =>
            "The InvoiceXML API rate limit was hit. Wait a few seconds before retrying. " +
            "If the limit is per-day, the user needs to upgrade their plan.",

        ToolFailureCategory.Server =>
            "The InvoiceXML API returned a server error. This may be transient; one " +
            "retry is reasonable. If it persists, surface the failure to the user.",

        ToolFailureCategory.Network =>
            "The MCP server could not establish a connection to the InvoiceXML API. " +
            "This is a server-side networking issue and cannot be resolved by retrying " +
            "with different request data. Surface the failure to the user.",

        _ =>
            "The request was rejected for an unexpected client-side reason. Retry with a " +
            "different request only if the failure message suggests a corrective change.",
    };
}
