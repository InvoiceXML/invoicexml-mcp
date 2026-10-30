using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that wraps <c>POST /v1/embed/{facturx|zugferd}/email</c>: embeds a CII
/// XML into a PDF exactly like <c>embed_invoice</c>, always with full validation, and
/// emails the hybrid PDF/A-3 instead of returning it.
/// </summary>
[McpServerToolType]
public sealed class EmailEmbeddedInvoiceTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public EmailEmbeddedInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "email_embedded_invoice", Title = "Embed XML into PDF and Email", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description(
        "Combine an existing PDF and an EN 16931 CII XML into a hybrid PDF/A-3 e-invoice and email it to the " +
        "recipient from the user's own verified domain, in one call. Pick 'format' = 'facturx' or 'zugferd'. " +
        "The XML MUST be a UN/CEFACT Cross Industry Invoice (CII) document; UBL is not accepted here " +
        "(convert it first with 'convert_invoice' ubl -> cii). The XML is always validated before anything is sent; " +
        "rules ['br-fr'] also checks the French e-invoicing rules (format 'facturx' only). " +
        ExtraRules.FacturXDefaultGuidance + "\n" +
        "\n" +
        "Provide the PDF via EXACTLY ONE of: pdfUrl (preferred) or pdfBase64 (small files only).\n" +
        "Provide the CII XML via EXACTLY ONE of: xml (text) or xmlUrl.\n" +
        "\n" +
        "Only use the ACTUAL bytes/text of the files. Never reconstruct, guess, or synthesize content. " +
        "If you cannot access a real file, ask the user for a public https:// URL or to paste it.\n" +
        "\n" +
        EmailTools.ConfirmationGuidance +
        "\n\n" +
        EmailTools.OutcomeGuidance)]
    public async Task<CallToolResult> EmailEmbeddedInvoiceAsync(
        [Description("Output hybrid format. Must be one of: facturx, zugferd.")]
        PdfInvoiceFormat format,

        [Description("The email: sender, recipient and, optionally, copies, reply-to address, subject and message.")]
        EmailMessage email,

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
        ArgumentNullException.ThrowIfNull(email);

        var slug = format.ToString().ToLowerInvariant();

        var rulesError = ExtraRules.Unsupported(rules, slug, "facturx");
        if (rulesError is not null)
            return ArtifactTools.InputError("INPUT-RULES", rulesError, ["rules"], slug);

        var (inputs, inputError) = await EmbedInputs
            .ResolveAsync(_fetcher, pdfUrl, pdfBase64, xml, xmlUrl, slug, cancellationToken)
            .ConfigureAwait(false);
        if (inputError is not null)
            return inputError;

        return await EmailTools.SendAsync(
            slug,
            email.To,
            () => _client.EmailEmbeddedAsync(format, inputs!.Pdf, inputs.CiiXml, email, rules, validationLanguage, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }
}
