using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that pulls content out of a hybrid PDF/A-3 e-invoice (Factur-X /
/// ZUGFeRD) via <c>POST /v1/extract/{json|xml}</c>: either the structured invoice
/// document as JSON, or the embedded EN 16931 CII XML.
/// </summary>
[McpServerToolType]
public sealed class ExtractInvoiceTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public ExtractInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "extract_invoice", Title = "Extract Invoice Data", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Extract content from a hybrid PDF/A-3 e-invoice (Factur-X or ZUGFeRD). " +
        "Choose 'target': 'json' for a structured invoice document (fields like seller, buyer, lines, totals), " +
        "or 'xml' for the raw embedded EN 16931 CII XML. " +
        "The PDF must contain an embedded XML attachment; if it doesn't, the result is an error. " +
        "\n\n" +
        "Provide the PDF via EXACTLY ONE of these inputs:\n" +
        "• pdfUrl — a public https:// URL to the PDF; the server downloads it. PREFER THIS whenever a URL exists.\n" +
        "• pdfBase64 — the PDF as base64. Only practical for small files; larger base64 gets corrupted in a tool call.\n" +
        "If you set neither or both, the result is an input error explaining what to fix.\n" +
        "\n" +
        "Only use the ACTUAL bytes of the file. Never reconstruct, guess, or synthesize a PDF. " +
        "If you cannot access the real file, ask the user for a public https:// URL (use pdfUrl) or to paste its base64.\n" +
        "\n" +
        "On success the result is a short summary plus the extracted JSON or XML inline as text. " +
        "On failure the result has isError=true and a JSON body with { success:false, failureCategory, errors[], guidance }.")]
    public async Task<CallToolResult> ExtractInvoiceAsync(
        [Description("What to extract: 'json' for a structured invoice document, 'xml' for the embedded CII XML.")]
        ExtractTarget target,

        CancellationToken cancellationToken,

        [Description("A public https:// URL to the PDF; the server fetches it. Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfUrl = null,

        [Description("The PDF as base64 (small files only). Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfBase64 = null)
    {
        var slug = target.ToString().ToLowerInvariant();

        var exclusive = ArtifactTools.ValidateExactlyOne(
        [
            ("pdfUrl", !string.IsNullOrWhiteSpace(pdfUrl)),
            ("pdfBase64", !string.IsNullOrWhiteSpace(pdfBase64)),
        ], slug);
        if (exclusive is not null)
            return exclusive;

        var (bytes, inputError) = await ResolvePdfAsync(pdfUrl, pdfBase64, slug, cancellationToken).ConfigureAwait(false);
        if (inputError is not null)
            return inputError;

        return await ArtifactTools.ExecuteAsync(
            slug,
            () => _client.ExtractAsync(target, bytes!, cancellationToken),
            artifact => target == ExtractTarget.Json
                ? $"Extracted a structured invoice document (JSON, {artifact.Content.Length:N0} bytes) from the PDF. The document is included inline below."
                : $"Extracted the embedded CII XML ({artifact.Content.Length:N0} bytes) from the PDF as {artifact.FileName}. The XML is included inline below.",
            cancellationToken).ConfigureAwait(false);
    }

    // Resolves the PDF from URL or base64 to bytes, applying the incomplete-PDF
    // safety net before any API call. Shared shape with the validate/convert tools.
    private async Task<(byte[]? Bytes, CallToolResult? Error)> ResolvePdfAsync(
        string? pdfUrl, string? pdfBase64, string slug, CancellationToken ct)
    {
        byte[] bytes;
        if (!string.IsNullOrWhiteSpace(pdfUrl))
        {
            var (fetched, error) = await ArtifactTools
                .FetchUrlAsync(_fetcher, pdfUrl, "pdfUrl", slug, ct)
                .ConfigureAwait(false);
            if (error is not null)
                return (null, error);
            bytes = fetched!;
        }
        else
        {
            try
            {
                bytes = Convert.FromBase64String(pdfBase64!);
            }
            catch (FormatException)
            {
                return (null, ArtifactTools.InputError("INPUT-BASE64",
                    "pdfBase64 is not valid base64. Use standard base64 (no chunking, no URL-safe alphabet). " +
                    "For anything but a small file, pass a public https:// URL via pdfUrl instead.",
                    ["pdfBase64"], slug));
            }
        }

        if (PdfSniffer.IsIncompletePdf(bytes))
        {
            return (null, ArtifactTools.InputError("INPUT-INCOMPLETE-PDF",
                $"Received {bytes.Length:N0} bytes that start like a PDF but have no %%EOF trailer — " +
                "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                ["pdfBase64", "pdfUrl"], slug));
        }

        return (bytes, null);
    }
}
