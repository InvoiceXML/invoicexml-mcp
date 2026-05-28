using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that renders a plain-XML e-invoice (UBL / CII / XRechnung) into a
/// human-readable PDF preview via <c>POST /v1/render/{format}/to/pdf</c>. The PDF
/// is a visual face only — it is NOT a hybrid Factur-X / ZUGFeRD document (use
/// <c>embed_invoice</c> for that).
/// </summary>
[McpServerToolType]
public sealed class RenderInvoiceTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public RenderInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "render_invoice", Title = "Render Invoice to PDF", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description(
        "Render a plain-XML e-invoice into a human-readable PDF preview. " +
        "Pick the matching 'format': 'ubl' for UBL 2.1 / Peppol BIS 3.0, 'cii' for UN/CEFACT CII, " +
        "'xrechnung' for German XRechnung. " +
        "The output is a VISUAL PDF only; it does NOT embed the XML. To produce a hybrid " +
        "Factur-X / ZUGFeRD PDF (visual face + embedded XML) use 'embed_invoice' or 'create_invoice' instead. " +
        "\n\n" +
        "Provide the XML via EXACTLY ONE of these inputs:\n" +
        "• xml — the XML document as text.\n" +
        "• xmlUrl — a public https:// URL to the XML; the server downloads it. PREFER THIS for large documents.\n" +
        "If you set neither or both, the result is an input error explaining what to fix.\n" +
        "\n" +
        "Only use the ACTUAL text of the file. Never reconstruct, guess, or synthesize invoice XML. " +
        "If you cannot access the real file, ask the user for a public https:// URL (use xmlUrl) or to paste the document.\n" +
        "\n" +
        "On success the result is a short summary plus the PDF as an embedded resource attachment; " +
        "refer to it by file name and do not attempt to read its bytes. On failure the result has isError=true " +
        "and a JSON body with { success:false, failureCategory, errors[], guidance }.")]
    public async Task<CallToolResult> RenderInvoiceAsync(
        [Description("Source XML profile. Must be one of: ubl, cii, xrechnung.")]
        XmlInvoiceFormat format,

        CancellationToken cancellationToken,

        [Description("The invoice XML as plain text. Provide exactly one of xml / xmlUrl.")]
        string? xml = null,

        [Description("A public https:// URL to the XML; the server fetches it. Provide exactly one of xml / xmlUrl.")]
        string? xmlUrl = null,

        [Description("Language of the human-readable PDF face: EN, DE, or FR. Defaults to EN.")]
        PdfLanguage language = PdfLanguage.EN)
    {
        var slug = format.ToString().ToLowerInvariant();

        var exclusive = ArtifactTools.ValidateExactlyOne(
        [
            ("xml", !string.IsNullOrWhiteSpace(xml)),
            ("xmlUrl", !string.IsNullOrWhiteSpace(xmlUrl)),
        ], slug);
        if (exclusive is not null)
            return exclusive;

        string xmlText;
        if (!string.IsNullOrWhiteSpace(xml))
        {
            xmlText = xml;
        }
        else
        {
            var (bytes, error) = await ArtifactTools
                .FetchUrlAsync(_fetcher, xmlUrl!, "xmlUrl", slug, cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
                return error;
            xmlText = ArtifactTools.DecodeUtf8(bytes!);
        }

        return await ArtifactTools.ExecuteAsync(
            slug,
            () => _client.RenderToPdfAsync(format, xmlText, language, cancellationToken),
            artifact =>
                $"Rendered {slug} XML to a PDF preview ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. " +
                "The PDF is delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes.",
            cancellationToken).ConfigureAwait(false);
    }
}
