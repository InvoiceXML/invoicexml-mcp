using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that embeds a CII XML into a PDF to produce a hybrid PDF/A-3 container
/// via <c>POST /v1/embed/{facturx|zugferd}</c>. Takes an existing visual PDF plus
/// the EN 16931 CII XML and returns the combined Factur-X / ZUGFeRD document.
/// </summary>
[McpServerToolType]
public sealed class EmbedInvoiceTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public EmbedInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "embed_invoice", Title = "Embed XML into Hybrid PDF", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description(
        "Combine an existing PDF and an EN 16931 CII XML into a hybrid PDF/A-3 e-invoice. " +
        "Pick 'format' = 'facturx' or 'zugferd' for the output branding. " +
        "The XML MUST be a UN/CEFACT Cross Industry Invoice (CII) document; UBL is not accepted here " +
        "(convert it first with 'convert_invoice' ubl -> cii). " +
        "\n\n" +
        "Provide the PDF via EXACTLY ONE of: pdfUrl (preferred) or pdfBase64 (small files only).\n" +
        "Provide the CII XML via EXACTLY ONE of: xml (text) or xmlUrl.\n" +
        "If a required pair is empty or has both set, the result is an input error explaining what to fix.\n" +
        "\n" +
        "Only use the ACTUAL bytes/text of the files. Never reconstruct, guess, or synthesize content. " +
        "If you cannot access a real file, ask the user for a public https:// URL or to paste it.\n" +
        "\n" +
        "On success the result is a short summary plus the hybrid PDF as an embedded resource attachment; " +
        "refer to it by file name and do not attempt to read its bytes. On failure the result has isError=true " +
        "and a JSON body with { success:false, failureCategory, errors[], guidance }.")]
    public async Task<CallToolResult> EmbedInvoiceAsync(
        [Description("Output hybrid format. Must be one of: facturx, zugferd.")]
        PdfInvoiceFormat format,

        CancellationToken cancellationToken,

        [Description("A public https:// URL to the PDF. Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfUrl = null,

        [Description("The PDF as base64 (small files only). Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfBase64 = null,

        [Description("The CII invoice XML as plain text. Provide exactly one of xml / xmlUrl.")]
        string? xml = null,

        [Description("A public https:// URL to the CII XML. Provide exactly one of xml / xmlUrl.")]
        string? xmlUrl = null)
    {
        var slug = format.ToString().ToLowerInvariant();

        var pdfExclusive = ArtifactTools.ValidateExactlyOne(
        [
            ("pdfUrl", !string.IsNullOrWhiteSpace(pdfUrl)),
            ("pdfBase64", !string.IsNullOrWhiteSpace(pdfBase64)),
        ], slug);
        if (pdfExclusive is not null)
            return pdfExclusive;

        var xmlExclusive = ArtifactTools.ValidateExactlyOne(
        [
            ("xml", !string.IsNullOrWhiteSpace(xml)),
            ("xmlUrl", !string.IsNullOrWhiteSpace(xmlUrl)),
        ], slug);
        if (xmlExclusive is not null)
            return xmlExclusive;

        byte[] pdfBytes;
        if (!string.IsNullOrWhiteSpace(pdfUrl))
        {
            var (fetched, error) = await ArtifactTools
                .FetchUrlAsync(_fetcher, pdfUrl, "pdfUrl", slug, cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
                return error;
            pdfBytes = fetched!;
        }
        else
        {
            try
            {
                pdfBytes = Convert.FromBase64String(pdfBase64!);
            }
            catch (FormatException)
            {
                return ArtifactTools.InputError("INPUT-BASE64",
                    "pdfBase64 is not valid base64. Use standard base64 (no chunking, no URL-safe alphabet). " +
                    "For anything but a small file, pass a public https:// URL via pdfUrl instead.",
                    ["pdfBase64"], slug);
            }
        }

        if (PdfSniffer.IsIncompletePdf(pdfBytes))
        {
            return ArtifactTools.InputError("INPUT-INCOMPLETE-PDF",
                $"Received {pdfBytes.Length:N0} bytes that start like a PDF but have no %%EOF trailer — " +
                "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                ["pdfBase64", "pdfUrl"], slug);
        }

        string ciiXml;
        if (!string.IsNullOrWhiteSpace(xml))
        {
            ciiXml = xml;
        }
        else
        {
            var (fetched, error) = await ArtifactTools
                .FetchUrlAsync(_fetcher, xmlUrl!, "xmlUrl", slug, cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
                return error;
            ciiXml = ArtifactTools.DecodeUtf8(fetched!);
        }

        return await ArtifactTools.ExecuteAsync(
            slug,
            () => _client.EmbedAsync(format, pdfBytes, ciiXml, cancellationToken),
            artifact =>
                $"Embedded the CII XML into a {slug} hybrid PDF/A-3 ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. " +
                "Delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes.",
            cancellationToken).ConfigureAwait(false);
    }
}
