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
        "The XML is validated before embedding; rules ['br-fr'] also checks the French e-invoicing rules " +
        "(format 'facturx' only). " + ExtraRules.FacturXDefaultGuidance + "\n" +
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
        string? xmlUrl = null,

        [Description(ExtraRules.ParameterDescription + " Format 'facturx' only.")]
        List<ExtraRuleset>? rules = null,

        [Description(ValidationLanguages.ParameterDescription + " Applies to the findings returned when the XML fails validation.")]
        ValidationLanguage? validationLanguage = null)
    {
        var slug = format.ToString().ToLowerInvariant();

        var rulesError = ExtraRules.Unsupported(rules, slug, "facturx");
        if (rulesError is not null)
            return ArtifactTools.InputError("INPUT-RULES", rulesError, ["rules"], slug);

        var (inputs, inputError) = await EmbedInputs
            .ResolveAsync(_fetcher, pdfUrl, pdfBase64, xml, xmlUrl, slug, cancellationToken)
            .ConfigureAwait(false);
        if (inputError is not null)
            return inputError;

        return await ArtifactTools.ExecuteAsync(
            slug,
            () => _client.EmbedAsync(format, inputs!.Pdf, inputs.CiiXml, rules, validationLanguage, cancellationToken),
            artifact =>
                $"Embedded the CII XML into a {slug} hybrid PDF/A-3 ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. " +
                "Delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes.",
            cancellationToken).ConfigureAwait(false);
    }
}
