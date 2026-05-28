using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool for deterministic format conversions across the EN 16931 family via
/// <c>POST /v1/convert/{source}/to/{target}</c>. Plain-XML sources (UBL / CII /
/// XRechnung) take XML input; hybrid-PDF sources (Factur-X / ZUGFeRD) take a PDF
/// whose embedded XML is transcoded. No AI is involved — every route is a known
/// transform. Only the 16 conversions the API actually exposes are accepted; the
/// tool rejects unsupported pairs client-side before spending an API credit.
/// </summary>
[McpServerToolType]
public sealed class ConvertInvoiceTool
{
    // The exact (source -> target) pairs exposed by the API's ConvertController.
    // Unsupported pairs (e.g. ubl -> ubl, or anything -> xrechnung from a PDF) are
    // rejected up front so the agent gets a clear reason instead of a 404 + credit.
    private static readonly HashSet<(InvoiceFormat Source, InvoiceFormat Target)> SupportedPairs =
    [
        // XML -> XML
        (InvoiceFormat.Cii, InvoiceFormat.Ubl),
        (InvoiceFormat.Ubl, InvoiceFormat.Cii),
        (InvoiceFormat.XRechnung, InvoiceFormat.Ubl),
        (InvoiceFormat.XRechnung, InvoiceFormat.Cii),
        (InvoiceFormat.Ubl, InvoiceFormat.XRechnung),
        (InvoiceFormat.Cii, InvoiceFormat.XRechnung),
        // hybrid PDF -> XML
        (InvoiceFormat.FacturX, InvoiceFormat.Ubl),
        (InvoiceFormat.FacturX, InvoiceFormat.Cii),
        (InvoiceFormat.Zugferd, InvoiceFormat.Ubl),
        (InvoiceFormat.Zugferd, InvoiceFormat.Cii),
        // XML -> hybrid PDF
        (InvoiceFormat.Cii, InvoiceFormat.FacturX),
        (InvoiceFormat.Cii, InvoiceFormat.Zugferd),
        (InvoiceFormat.Ubl, InvoiceFormat.FacturX),
        (InvoiceFormat.Ubl, InvoiceFormat.Zugferd),
        (InvoiceFormat.XRechnung, InvoiceFormat.FacturX),
        (InvoiceFormat.XRechnung, InvoiceFormat.Zugferd),
    ];

    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public ConvertInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "convert_invoice", Title = "Convert Invoice Format", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description(
        "Convert an e-invoice from one format to another (a deterministic syntax transform, no AI). " +
        "Set 'sourceFormat' and 'targetFormat', each one of: ubl, cii, xrechnung, facturx, zugferd. " +
        "Supported conversions: ubl<->cii, xrechnung->ubl/cii, ubl/cii->xrechnung, " +
        "facturx/zugferd->ubl/cii (extract + transcode the embedded XML), and " +
        "ubl/cii/xrechnung->facturx/zugferd (render + embed). Other pairs are rejected with an input error.\n" +
        "\n" +
        "Provide the source document by its type:\n" +
        "• If sourceFormat is ubl / cii / xrechnung (an XML format): use xml (text) or xmlUrl.\n" +
        "• If sourceFormat is facturx / zugferd (a hybrid PDF): use pdfBase64 or pdfUrl (prefer pdfUrl).\n" +
        "Provide EXACTLY ONE input, and it must match the source type. Mismatches and missing/duplicate inputs " +
        "return an input error explaining what to fix.\n" +
        "\n" +
        "Only use the ACTUAL bytes/text of the file. Never reconstruct, guess, or synthesize content. " +
        "If you cannot access the real file, ask the user for a public https:// URL or to paste it.\n" +
        "\n" +
        "On success the result is a short summary plus the converted document: XML targets are returned inline as text, " +
        "PDF (facturx/zugferd) targets as an embedded resource attachment. On failure the result has isError=true " +
        "and a JSON body with { success:false, failureCategory, errors[], guidance }.")]
    public async Task<CallToolResult> ConvertInvoiceAsync(
        [Description("Source format. One of: ubl, cii, xrechnung, facturx, zugferd.")]
        InvoiceFormat sourceFormat,

        [Description("Target format. One of: ubl, cii, xrechnung, facturx, zugferd.")]
        InvoiceFormat targetFormat,

        CancellationToken cancellationToken,

        [Description("For an XML source (ubl/cii/xrechnung): the source XML as text.")]
        string? xml = null,

        [Description("For an XML source (ubl/cii/xrechnung): a public https:// URL to the XML.")]
        string? xmlUrl = null,

        [Description("For a hybrid-PDF source (facturx/zugferd): the source PDF as base64 (small files only).")]
        string? pdfBase64 = null,

        [Description("For a hybrid-PDF source (facturx/zugferd): a public https:// URL to the PDF.")]
        string? pdfUrl = null)
    {
        var sourceSlug = sourceFormat.ToString().ToLowerInvariant();
        var targetSlug = targetFormat.ToString().ToLowerInvariant();
        var slug = $"{sourceSlug}->{targetSlug}";

        if (!SupportedPairs.Contains((sourceFormat, targetFormat)))
        {
            return ArtifactTools.InputError("INPUT-UNSUPPORTED-CONVERSION",
                $"Conversion {sourceSlug} -> {targetSlug} is not supported. Supported pairs: " +
                "ubl<->cii, xrechnung->ubl/cii, ubl/cii->xrechnung, facturx/zugferd->ubl/cii, " +
                "and ubl/cii/xrechnung->facturx/zugferd.",
                ["sourceFormat", "targetFormat"], slug);
        }

        var xmlSource = sourceFormat is InvoiceFormat.Ubl or InvoiceFormat.Cii or InvoiceFormat.XRechnung;

        var exclusive = ArtifactTools.ValidateExactlyOne(
        [
            ("xml", !string.IsNullOrWhiteSpace(xml)),
            ("xmlUrl", !string.IsNullOrWhiteSpace(xmlUrl)),
            ("pdfBase64", !string.IsNullOrWhiteSpace(pdfBase64)),
            ("pdfUrl", !string.IsNullOrWhiteSpace(pdfUrl)),
        ], slug);
        if (exclusive is not null)
            return exclusive;

        var providedXml = !string.IsNullOrWhiteSpace(xml) || !string.IsNullOrWhiteSpace(xmlUrl);
        var providedPdf = !string.IsNullOrWhiteSpace(pdfBase64) || !string.IsNullOrWhiteSpace(pdfUrl);

        if (xmlSource && providedPdf)
        {
            return ArtifactTools.InputError("INPUT-SOURCE-MISMATCH",
                $"Source format '{sourceSlug}' is an XML format; provide the source via xml or xmlUrl, not a PDF input.",
                ["xml", "xmlUrl"], slug);
        }
        if (!xmlSource && providedXml)
        {
            return ArtifactTools.InputError("INPUT-SOURCE-MISMATCH",
                $"Source format '{sourceSlug}' is a hybrid PDF; provide the source via pdfBase64 or pdfUrl, not an XML input.",
                ["pdfBase64", "pdfUrl"], slug);
        }

        byte[] content;
        string contentType;
        string fileName;

        if (xmlSource)
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

            content = pdfBytes;
            contentType = "application/pdf";
            fileName = "invoice.pdf";
        }

        var targetIsPdf = targetFormat is InvoiceFormat.FacturX or InvoiceFormat.Zugferd;

        return await ArtifactTools.ExecuteAsync(
            slug,
            () => _client.ConvertAsync(sourceFormat, targetFormat, content, contentType, fileName, cancellationToken),
            artifact => targetIsPdf
                ? $"Converted {sourceSlug} to {targetSlug} ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. " +
                  "Delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes."
                : $"Converted {sourceSlug} to {targetSlug} ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. The {targetSlug} XML is included inline below.",
            cancellationToken).ConfigureAwait(false);
    }
}
