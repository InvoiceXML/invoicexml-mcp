using System.Text;
using System.Text.Json;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Shared helpers for assembling <see cref="CallToolResult"/> values from tool
/// outputs. Every tool that produces an artifact (XML or binary blob) should
/// build its result through <see cref="ForArtifact"/> so the LLM sees a
/// summary plus the artifact in the right MCP content slot: text inline for
/// textual artifacts the LLM may want to quote, an
/// <see cref="EmbeddedResourceBlock"/> for binary artifacts the client should
/// render as a downloadable attachment without feeding the bytes into the
/// LLM's context window.
/// Tools that surface a structured failure go through <see cref="ForFailure"/>,
/// which also flips <see cref="CallToolResult.IsError"/> so agent frameworks
/// can apply retry policy.
/// </summary>
/// <remarks>
/// This class is the single packaging seam for every tool that emits an
/// artifact: <c>create_invoice</c> today and any future
/// <c>convert_*</c> / <c>render_*</c> / <c>embed_*</c> tool tomorrow. Adding
/// a new artifact-producing tool should not require touching this file.
/// </remarks>
internal static class ToolResults
{
    private const string AttachmentUriScheme = "attachment://";

    /// <summary>
    /// Packages an artifact returned by an API call as a single-shot MCP tool result.
    /// </summary>
    /// <param name="summary">
    /// Short, human-readable description of the artifact. Always emitted as
    /// the first <see cref="TextContentBlock"/> and is the only thing the LLM
    /// is expected to "read" for binary artifacts.
    /// </param>
    /// <param name="content">Raw bytes of the artifact as the API returned them.</param>
    /// <param name="contentType">
    /// MIME type that drives the choice of second content block. Textual
    /// types (<c>application/xml</c>, <c>application/json</c>, <c>text/*</c>,
    /// <c>application/xhtml+xml</c>) ship inline as a second
    /// <see cref="TextContentBlock"/>; everything else ships as an
    /// <see cref="EmbeddedResourceBlock"/>.
    /// </param>
    /// <param name="fileName">
    /// Suggested file name; embedded into the resource URI fragment so the
    /// client can present a sensible "Save as…" default for attachments.
    /// </param>
    public static CallToolResult ForArtifact(
        string summary,
        byte[] content,
        string contentType,
        string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var blocks = new List<ContentBlock>(capacity: 2)
        {
            new TextContentBlock { Text = summary },
        };

        if (IsTextual(contentType))
        {
            blocks.Add(new TextContentBlock
            {
                Text = Encoding.UTF8.GetString(content),
            });
        }
        else
        {
            // FromBytes handles the base64 encoding internally; Blob is exposed
            // as a UTF-8 byte buffer (the base64 string in bytes) for perf, not
            // a plain string, so we avoid touching it directly.
            blocks.Add(new EmbeddedResourceBlock
            {
                Resource = BlobResourceContents.FromBytes(
                    bytes: content,
                    uri: AttachmentUriScheme + fileName,
                    mimeType: contentType),
            });
        }

        return new CallToolResult
        {
            Content = blocks,
            IsError = false,
        };
    }

    /// <summary>
    /// Packages a structured failure payload as a tool result with
    /// <see cref="CallToolResult.IsError"/> set to <see langword="true"/>.
    /// The payload is JSON-serialised via
    /// <see cref="InvoiceXmlJsonOptions.Default"/> (camelCase, ignore null)
    /// into a single <see cref="TextContentBlock"/> so the LLM can parse it
    /// and decide whether to retry.
    /// </summary>
    /// <typeparam name="TPayload">Any serialisable type; tools define their own shape.</typeparam>
    public static CallToolResult ForFailure<TPayload>(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var json = JsonSerializer.Serialize(payload, InvoiceXmlJsonOptions.Default);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }],
            IsError = true,
        };
    }

    private static bool IsTextual(string contentType) =>
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
}
