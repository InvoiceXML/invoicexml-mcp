using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that validates hybrid PDF/A-3 e-invoices (Factur-X, ZUGFeRD) via
/// <c>/v1/validate/{format}</c>. The API extracts the embedded XML server-side
/// and validates it against the conformance profile it declares (MINIMUM
/// through EXTENDED, or XRechnung), using the official per-profile rules.
/// </summary>
[McpServerToolType]
public sealed class ValidatePdfInvoiceTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public ValidatePdfInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "validate_pdf_invoice", Title = "Validate PDF Invoice", ReadOnly = true, OpenWorld = true, UseStructuredContent = true)]
    [Description(
        "Validate a hybrid PDF/A-3 e-invoice that has an EN 16931 CII XML embedded inside: Factur-X or ZUGFeRD. " +
        "Pick 'format' = 'facturx' or 'zugferd' (both run the same validation pipeline; pick the one the user named). " +
        "Use THIS tool for PDF invoices; for plain XML (UBL / CII / XRechnung) use 'validate_xml_invoice'. " +
        "\n\n" +
        "Provide the PDF via EXACTLY ONE of these inputs:\n" +
        "• pdfUrl: a public https:// URL to the PDF; the server downloads it. PREFER THIS whenever a URL exists.\n" +
        "• pdfBase64: the PDF as base64. Only practical for small files (a few tens of KB); larger base64 gets " +
        "corrupted when written into a tool call, so use a URL instead.\n" +
        "If you set neither or both, the result is valid=false with an INPUT-… error explaining what to fix.\n" +
        "\n" +
        "rules ['br-fr'] also checks the French e-invoicing rules (format 'facturx' only). " +
        ExtraRules.FacturXDefaultGuidance + "\n" +
        "\n" +
        "Only use the ACTUAL bytes of the file. Never reconstruct, guess, or synthesize a PDF. " +
        "If you cannot access the real file (e.g. a user uploaded it and you can't read its bytes), do NOT call " +
        "this tool with made-up content; ask the user for a public https:// URL (use pdfUrl) or to paste the file's base64.\n" +
        "\n" +
        "The embedded XML is located under any spec attachment name (factur-x.xml, the legacy zugferd-invoice.xml, " +
        "or xrechnung.xml for the ZUGFeRD XRechnung reference profile) and validated against the conformance " +
        "profile it declares (MINIMUM, BASIC WL, BASIC, EN 16931, EXTENDED, or XRechnung), using the official " +
        "per-profile rules. The response reports the applied profile in data.profile and the declared identifier " +
        "in data.customizationId; every finding carries a 'layer' field (xsd, en16931, or cius). " +
        "\n\n" +
        "The result has a 'valid' field. On valid=true the embedded invoice is compliant ('warnings' may carry " +
        "non-blocking issues; a PROFILE-SCOPE warning means a MINIMUM / BASIC WL document that does not qualify " +
        "as an e-invoice under German B2B rules). On valid=false the 'errors' array explains what was wrong: " +
        "PDF-EMBED when no XML is embedded, PDF-EMBED-SYNTAX when the attachment is UBL instead of CII " +
        "(suggest validate_xml_invoice with format 'ubl' for the extracted file), or specific rule failures. " +
        "Surface these to the user.")]
    public async Task<ValidationResult> ValidatePdfAsync(
        [Description("Validation endpoint. Must be one of: facturx, zugferd (same pipeline; pick the one the user named). The conformance profile is detected automatically from the embedded XML.")]
        PdfInvoiceFormat format,

        CancellationToken cancellationToken,

        [Description("A public https:// URL to the PDF; the server fetches it. Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfUrl = null,

        [Description("The PDF as base64 (small files only). Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfBase64 = null,

        [Description(ExtraRules.ParameterDescription + " Format 'facturx' only.")]
        List<ExtraRuleset>? rules = null)
    {
        var rulesError = ExtraRules.Unsupported(rules, format.ToString().ToLowerInvariant(), "facturx");
        if (rulesError is not null)
            return FileInputResolver.InputError("INPUT-RULES", rulesError, ["rules"]);

        var exclusive = FileInputResolver.ValidateExactlyOne(
        [
            ("pdfUrl", !string.IsNullOrWhiteSpace(pdfUrl)),
            ("pdfBase64", !string.IsNullOrWhiteSpace(pdfBase64)),
        ]);
        if (exclusive is not null)
            return exclusive;

        byte[] bytes;

        if (!string.IsNullOrWhiteSpace(pdfUrl))
        {
            var (fetched, error) = await FileInputResolver
                .FetchUrlAsync(_fetcher, pdfUrl, "pdfUrl", cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
                return error;
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
                return FileInputResolver.InputError("INPUT-BASE64",
                    "pdfBase64 is not valid base64. Use standard base64 (no chunking, no URL-safe alphabet). " +
                    "For anything but a small file, pass a public https:// URL via pdfUrl instead.",
                    ["pdfBase64"]);
            }
        }

        // Safety net: catch truncated / fabricated PDFs (a %PDF header with no %%EOF
        // trailer) before spending an API credit. Gated on the %PDF prefix so non-PDF
        // payloads are passed straight through; the API gives the better error there.
        if (PdfSniffer.IsIncompletePdf(bytes))
        {
            return FileInputResolver.InputError("INPUT-INCOMPLETE-PDF",
                $"Received {bytes.Length:N0} bytes that start like a PDF but have no %%EOF trailer; " +
                "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                ["pdfBase64", "pdfUrl"]);
        }

        try
        {
            return await _client.ValidatePdfAsync(format, bytes, rules, cancellationToken).ConfigureAwait(false);
        }
        catch (InvoiceXmlApiException ex)
        {
            return ValidateXmlInvoiceTool.SynthesizeFailureResult(ToolFailure.FromApiException(ex));
        }
        catch (HttpRequestException ex)
        {
            return ValidateXmlInvoiceTool.SynthesizeFailureResult(ToolFailure.FromNetworkException(ex));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return ValidateXmlInvoiceTool.SynthesizeFailureResult(ToolFailure.FromNetworkException(ex));
        }
    }
}
