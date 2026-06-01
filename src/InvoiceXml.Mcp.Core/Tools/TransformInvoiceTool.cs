using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that transforms ANY PDF invoice into an EN 16931 e-invoice via
/// <c>POST /v1/transform/to/{target}</c>. Unlike <c>convert_invoice</c> (which only
/// transcodes the XML already embedded in a hybrid Factur-X / ZUGFeRD PDF), this runs
/// data extraction over the PDF, so it also handles a plain (non-hybrid) invoice PDF.
/// XML targets (ubl / cii / xrechnung) return XML; hybrid-PDF targets (facturx /
/// zugferd) return a PDF/A-3 with the XML embedded.
/// </summary>
[McpServerToolType]
public sealed class TransformInvoiceTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public TransformInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "transform_invoice", Title = "Transform PDF to E-Invoice", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description(
        "Turn a PDF invoice into a structured EN 16931 e-invoice by extracting its data and rebuilding it. " +
        "USE THIS when the source is a PDF that does NOT already contain embedded e-invoice XML " +
        "(an ordinary/visual invoice PDF), or when you are unsure whether the PDF is a hybrid. " +
        "It reads the invoice data out of the PDF and produces the target format. " +
        "(For a PDF that ALREADY embeds Factur-X / ZUGFeRD XML, convert_invoice is the deterministic, " +
        "lossless choice; transform_invoice still works but re-derives the data.)\n" +
        "\n" +
        "Set 'targetFormat' to one of: ubl, cii, xrechnung, facturx, zugferd. XML targets " +
        "(ubl/cii/xrechnung) return XML; hybrid-PDF targets (facturx/zugferd) return a PDF/A-3.\n" +
        "\n" +
        "Provide the source PDF via EXACTLY ONE of these inputs:\n" +
        "• pdfUrl — a public https:// URL to the PDF; the server downloads it. PREFER THIS whenever a URL exists.\n" +
        "• pdfBase64 — the PDF as base64. Only practical for small files; larger base64 gets corrupted in a tool call.\n" +
        "If you set neither or both, the result is an input error explaining what to fix.\n" +
        "\n" +
        "For a facturx / zugferd target, 'language' sets the human-readable PDF face (EN, DE, FR; default EN). " +
        "For an xrechnung target, 'buyerReference' supplies the BT-10 buyer reference (Leitweg-ID) XRechnung requires.\n" +
        "\n" +
        "Only use the ACTUAL bytes of the file. Never reconstruct, guess, or synthesize a PDF. " +
        "If you cannot access the real file, ask the user for a public https:// URL (use pdfUrl) or to paste its base64.\n" +
        "\n" +
        "On success the result is a short summary plus the document: XML targets inline as text, " +
        "PDF (facturx/zugferd) targets as an embedded resource attachment. On failure the result has isError=true " +
        "and a JSON body with { success:false, failureCategory, errors[], guidance, invoiceData? }.\n" +
        "\n" +
        "RECOVERY: when failureCategory is 'Validation' the body also carries 'invoiceData', the EN 16931 " +
        "invoice this tool managed to extract from the PDF. Do NOT just retry transform_invoice with the same PDF " +
        "(it re-extracts and fails the same way), and this tool has no field arguments to push corrections into. " +
        "Instead tell the user which fields in 'errors' are missing or invalid; once they supply the values, merge " +
        "them into the 'invoiceData' object and call create_invoice with that document and this same target format.")]
    public async Task<CallToolResult> TransformInvoiceAsync(
        [Description("Target format. One of: ubl, cii, xrechnung, facturx, zugferd.")]
        InvoiceFormat targetFormat,

        CancellationToken cancellationToken,

        [Description("A public https:// URL to the source PDF; the server fetches it. Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfUrl = null,

        [Description("The source PDF as base64 (small files only). Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfBase64 = null,

        [Description("For a facturx / zugferd target: language of the human-readable PDF face. EN, DE, or FR. Defaults to EN.")]
        PdfLanguage language = PdfLanguage.EN,

        [Description("For an xrechnung target: the BT-10 buyer reference (Leitweg-ID) required by XRechnung.")]
        string? buyerReference = null)
    {
        var slug = targetFormat.ToString().ToLowerInvariant();

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

        var targetIsPdf = targetFormat is InvoiceFormat.FacturX or InvoiceFormat.Zugferd;

        return await ArtifactTools.ExecuteAsync(
            slug,
            () => _client.TransformAsync(targetFormat, bytes!, language, buyerReference, cancellationToken),
            artifact => targetIsPdf
                ? $"Transformed the source PDF into a {slug} invoice ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. " +
                  "Delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes."
                : $"Transformed the source PDF into a {slug} invoice ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. The {slug} XML is included inline below.",
            cancellationToken).ConfigureAwait(false);
    }

    // Resolves the PDF from URL or base64 to bytes, applying the incomplete-PDF
    // safety net before any API call. Shared shape with the extract/convert tools.
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
