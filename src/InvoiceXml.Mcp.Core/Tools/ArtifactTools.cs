using System.Text;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Shared plumbing for the artifact-producing tools (<c>render_invoice</c>,
/// <c>extract_invoice</c>, <c>embed_invoice</c>, <c>convert_invoice</c>), which all
/// return a <see cref="CallToolResult"/> wrapping either an artifact or a structured
/// failure. This is the single seam for: input arbitration (reusing
/// <see cref="FileInputResolver"/>), API-exception translation (reusing
/// <see cref="ToolFailure"/>), and result packaging (reusing <see cref="ToolResults"/>),
/// so the four tools never duplicate that policy.
/// </summary>
/// <remarks>
/// The validation tools deliberately do NOT route through here: they return a
/// <see cref="ValidationResult"/> rather than a <see cref="CallToolResult"/>. This
/// helper adapts the <see cref="ValidationResult"/>-shaped errors produced by
/// <see cref="FileInputResolver"/> into the <see cref="ToolFailurePayload"/> envelope.
/// </remarks>
internal static class ArtifactTools
{
    /// <summary>
    /// Verifies exactly one of the supplied input modes is set. Returns
    /// <see langword="null"/> when satisfied, otherwise a ready-to-return failure result.
    /// </summary>
    public static CallToolResult? ValidateExactlyOne((string Name, bool Provided)[] modes, string? formatSlug)
    {
        var error = FileInputResolver.ValidateExactlyOne(modes);
        return error is null ? null : ToInputFailure(error, formatSlug);
    }

    /// <summary>Fetches a URL to bytes, mapping fetch failures to an <c>INPUT-URL</c> failure result.</summary>
    public static async Task<(byte[]? Bytes, CallToolResult? Error)> FetchUrlAsync(
        IRemoteFileFetcher fetcher, string url, string fieldName, string? formatSlug, CancellationToken ct)
    {
        var (bytes, error) = await FileInputResolver.FetchUrlAsync(fetcher, url, fieldName, ct).ConfigureAwait(false);
        return (bytes, error is null ? null : ToInputFailure(error, formatSlug));
    }

    /// <summary>Builds a client-side input failure result carrying a single <c>INPUT-…</c> finding.</summary>
    public static CallToolResult InputError(string rule, string message, IReadOnlyList<string> fields, string? formatSlug = null) =>
        ToInputFailure(FileInputResolver.InputError(rule, message, fields), formatSlug);

    /// <summary>
    /// Runs the API call and packages the outcome: a success artifact via
    /// <see cref="ToolResults.ForArtifact"/>, or a translated failure via
    /// <see cref="ToolResults.ForFailure{TPayload}"/>. Mirrors the exception policy
    /// used by <c>create_invoice</c> so every artifact tool behaves identically.
    /// </summary>
    public static async Task<CallToolResult> ExecuteAsync(
        string? formatSlug,
        Func<Task<DocumentArtifact>> call,
        Func<DocumentArtifact, string> summary,
        CancellationToken ct)
    {
        try
        {
            var artifact = await call().ConfigureAwait(false);
            return ToolResults.ForArtifact(summary(artifact), artifact.Content, artifact.ContentType, artifact.FileName);
        }
        catch (InvoiceXmlApiException ex)
        {
            return Failure(formatSlug, ToolFailure.FromApiException(ex));
        }
        catch (HttpRequestException ex)
        {
            return Failure(formatSlug, ToolFailure.FromNetworkException(ex));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeouts surface as TaskCanceledException with no caller cancellation.
            return Failure(formatSlug, ToolFailure.FromNetworkException(ex));
        }
    }

    /// <summary>Decodes downloaded bytes as UTF-8, stripping a leading BOM the API's XML parser would choke on.</summary>
    public static string DecodeUtf8(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
    }

    private static CallToolResult Failure(string? formatSlug, ToolFailure.Translation t) =>
        ToolResults.ForFailure(new ToolFailurePayload
        {
            Success = false,
            Summary = t.Summary,
            Format = formatSlug,
            FailureCategory = t.Category,
            StatusCode = t.StatusCode,
            Errors = t.Errors,
            Guidance = t.Guidance,
        });

    private static CallToolResult ToInputFailure(ValidationResult error, string? formatSlug)
    {
        var finding = error.Errors is { Count: > 0 } ? error.Errors[0] : null;
        return ToolResults.ForFailure(new ToolFailurePayload
        {
            Success = false,
            Summary = error.Detail ?? finding?.Message ?? "Invalid input.",
            Format = formatSlug,
            FailureCategory = null,
            StatusCode = null,
            Errors = error.Errors,
            Guidance = "This is a client-side input problem; correct the input and call this tool again.",
        });
    }
}
