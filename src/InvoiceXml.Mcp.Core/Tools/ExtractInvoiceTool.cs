using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that pulls content out of an e-invoice via
/// <c>POST /v1/extract/{json|xml|attachments}</c>: the structured invoice
/// document as JSON, the embedded EN 16931 CII XML, or the embedded supporting
/// documents as a ZIP. The json/xml targets read a hybrid PDF/A-3 (Factur-X /
/// ZUGFeRD); the attachments target also accepts a plain CII / UBL invoice XML.
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
        "Extract content from an e-invoice. " +
        "Choose 'target': 'json' for a structured invoice document as an { \"invoice\": { ... } } envelope " +
        "(fields like seller, buyer, lines, totals sit under the invoice key), " +
        "'xml' for the raw embedded EN 16931 CII XML, " +
        "or 'attachments' for every embedded supporting document (BG-24 attachments with a BT-125 payload, e.g. " +
        "timesheets or delivery notes) bundled as a ZIP archive. Documents referenced only by external URI " +
        "(BT-124) are not downloaded, and an invoice with no embedded attachments returns an error saying so. " +
        "\n\n" +
        "Targets 'json' and 'xml' read a hybrid PDF/A-3 e-invoice (Factur-X or ZUGFeRD); the PDF must contain an " +
        "embedded XML attachment, otherwise the result is an error. Target 'attachments' additionally accepts a " +
        "plain CII or UBL invoice XML, since XML invoices can carry embedded attachments too. " +
        "\n\n" +
        "Provide the document via EXACTLY ONE of these inputs:\n" +
        "• pdfUrl: a public https:// URL to the PDF; the server downloads it. PREFER THIS whenever a URL exists.\n" +
        "• pdfBase64: the PDF as base64. Only practical for small files; larger base64 gets corrupted in a tool call.\n" +
        "• xml: the invoice XML as text (target 'attachments' only).\n" +
        "• xmlUrl: a public https:// URL to the XML (target 'attachments' only).\n" +
        "If you set none or several, the result is an input error explaining what to fix.\n" +
        "\n" +
        "Only use the ACTUAL bytes/text of the file. Never reconstruct, guess, or synthesize a document. " +
        "If you cannot access the real file, ask the user for a public https:// URL or to paste it.\n" +
        "\n" +
        "On success the result is a short summary plus the extracted JSON or XML inline as text; target 'attachments' " +
        "delivers the ZIP as an embedded resource attachment instead (refer to it by file name, do not read its bytes). " +
        "On failure the result has isError=true and a JSON body with { success:false, failureCategory, errors[], guidance }.")]
    public async Task<CallToolResult> ExtractInvoiceAsync(
        [Description("What to extract: 'json' for a structured invoice document, 'xml' for the embedded CII XML, 'attachments' for the embedded supporting documents as a ZIP.")]
        ExtractTarget target,

        CancellationToken cancellationToken,

        [Description("A public https:// URL to the PDF; the server fetches it. Provide exactly one input.")]
        string? pdfUrl = null,

        [Description("The PDF as base64 (small files only). Provide exactly one input.")]
        string? pdfBase64 = null,

        [Description("For target 'attachments' only: the invoice XML (CII or UBL) as text. Provide exactly one input.")]
        string? xml = null,

        [Description("For target 'attachments' only: a public https:// URL to the invoice XML. Provide exactly one input.")]
        string? xmlUrl = null)
    {
        var slug = target.ToString().ToLowerInvariant();

        var exclusive = ArtifactTools.ValidateExactlyOne(
        [
            ("pdfUrl", !string.IsNullOrWhiteSpace(pdfUrl)),
            ("pdfBase64", !string.IsNullOrWhiteSpace(pdfBase64)),
            ("xml", !string.IsNullOrWhiteSpace(xml)),
            ("xmlUrl", !string.IsNullOrWhiteSpace(xmlUrl)),
        ], slug);
        if (exclusive is not null)
            return exclusive;

        var providedXml = !string.IsNullOrWhiteSpace(xml) || !string.IsNullOrWhiteSpace(xmlUrl);
        if (providedXml && target != ExtractTarget.Attachments)
        {
            return ArtifactTools.InputError("INPUT-SOURCE-MISMATCH",
                $"Target '{slug}' extracts from a hybrid PDF; provide the document via pdfUrl or pdfBase64. " +
                "XML input (xml / xmlUrl) is only accepted by target 'attachments'.",
                ["xml", "xmlUrl"], slug);
        }

        byte[] content;
        string contentType;
        string fileName;

        if (providedXml)
        {
            string xmlText;
            if (!string.IsNullOrWhiteSpace(xml))
            {
                xmlText = xml;
            }
            else
            {
                var (fetched, error) = await ArtifactTools
                    .FetchUrlAsync(_fetcher, xmlUrl!, "xmlUrl", slug, cancellationToken)
                    .ConfigureAwait(false);
                if (error is not null)
                    return error;
                xmlText = ArtifactTools.DecodeUtf8(fetched!);
            }

            content = System.Text.Encoding.UTF8.GetBytes(xmlText);
            contentType = "application/xml";
            fileName = "invoice.xml";
        }
        else
        {
            var (bytes, inputError) = await ResolvePdfAsync(pdfUrl, pdfBase64, slug, cancellationToken).ConfigureAwait(false);
            if (inputError is not null)
                return inputError;

            content = bytes!;
            contentType = "application/pdf";
            fileName = "invoice.pdf";
        }

        return await ArtifactTools.ExecuteAsync(
            slug,
            () => _client.ExtractAsync(target, content, contentType, fileName, cancellationToken),
            artifact => target switch
            {
                ExtractTarget.Json =>
                    $"Extracted a structured invoice document (JSON, {artifact.Content.Length:N0} bytes) from the PDF. " +
                    "The response is included inline below; the document fields sit under its 'invoice' key.",
                ExtractTarget.Attachments =>
                    $"Extracted the embedded supporting documents from the invoice as {artifact.FileName} ({artifact.Content.Length:N0} bytes). " +
                    "The ZIP is delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes.",
                _ =>
                    $"Extracted the embedded CII XML ({artifact.Content.Length:N0} bytes) from the PDF as {artifact.FileName}. The XML is included inline below.",
            },
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
                $"Received {bytes.Length:N0} bytes that start like a PDF but have no %%EOF trailer: " +
                "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                ["pdfBase64", "pdfUrl"], slug));
        }

        return (bytes, null);
    }
}
