using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that runs the standard validation pipeline and returns a printable
/// PDF compliance report via <c>POST /v1/validate/{format}/report</c>. The
/// verdict is read from the <c>X-Invoice-Valid</c> response header and stated
/// in the summary; the PDF itself ships as an embedded resource attachment.
/// For a machine-readable verdict use <c>validate_xml_invoice</c> /
/// <c>validate_pdf_invoice</c> instead.
/// </summary>
[McpServerToolType]
public sealed class RenderValidationReportTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public RenderValidationReportTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "render_validation_report", Title = "Render Validation Report PDF", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Validate an e-invoice and return the result as a printable PDF compliance report: verdict banner, " +
        "summary chips, document-level findings, and the field-by-field EN 16931 business term table. " +
        "Runs the exact same validation pipeline as validate_xml_invoice / validate_pdf_invoice; use those tools " +
        "when you need the machine-readable JSON verdict to reason over, and THIS tool when the user wants a " +
        "report document to save, share, or archive. " +
        "Set 'format' to match the document: 'ubl', 'cii', 'xrechnung' for plain XML; 'facturx', 'zugferd' for " +
        "hybrid PDFs; 'order-x' for an Order-X purchase order (hybrid PDF or plain Cross-Industry Order XML). " +
        "\n\n" +
        "Provide the document by its type:\n" +
        "• If format is ubl / cii / xrechnung (an XML format): use xml (text) or xmlUrl.\n" +
        "• If format is facturx / zugferd (a hybrid PDF): use pdfBase64 or pdfUrl (prefer pdfUrl).\n" +
        "• If format is order-x: any one of the four inputs, matching what the file is.\n" +
        "Provide EXACTLY ONE input, and it must match the format type. Mismatches and missing/duplicate inputs " +
        "return an input error explaining what to fix.\n" +
        "\n" +
        "Optional: rules ['br-fr'] adds the French e-invoicing rules (formats 'ubl', 'cii', 'facturx'), and " +
        "footerBrand 'none' removes the InvoiceXML credit from the report footer. " +
        ExtraRules.FacturXDefaultGuidance + " For 'cii' and 'ubl': " + ExtraRules.AskForFrenchNetworkGuidance + "\n" +
        "\n" +
        "Only use the ACTUAL bytes/text of the file. Never reconstruct, guess, or synthesize content. " +
        "If you cannot access the real file, ask the user for a public https:// URL or to paste it.\n" +
        "\n" +
        "On success the result is a short summary stating the verdict (valid / not compliant) plus the PDF report " +
        "as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes. " +
        "Validation failures still produce a report PDF (that is the point); only transport-level problems " +
        "(missing file, malformed XML, auth) return isError=true with a JSON body " +
        "{ success:false, failureCategory, errors[], guidance }.")]
    public async Task<CallToolResult> RenderValidationReportAsync(
        [Description("Validation endpoint matching the document. One of: ubl, cii, xrechnung (XML), facturx, zugferd (hybrid PDF), order-x (Order-X PDF or Cross-Industry Order XML).")]
        ValidationReportFormat format,

        CancellationToken cancellationToken,

        [Description("For an XML document (ubl/cii/xrechnung, or an order-x CIO XML): the XML as text.")]
        string? xml = null,

        [Description("For an XML document (ubl/cii/xrechnung, or an order-x CIO XML): a public https:// URL to the XML.")]
        string? xmlUrl = null,

        [Description("For a hybrid PDF (facturx/zugferd, or an order-x PDF): the PDF as base64 (small files only).")]
        string? pdfBase64 = null,

        [Description("For a hybrid PDF (facturx/zugferd, or an order-x PDF): a public https:// URL to the PDF.")]
        string? pdfUrl = null,

        [Description(ExtraRules.ParameterDescription + " Formats 'ubl', 'cii' and 'facturx' only.")]
        List<ExtraRuleset>? rules = null,

        [Description("Footer credit of the report PDF: 'invoicexml' (default) or 'none' to omit it.")]
        FooterBrand? footerBrand = null)
    {
        var slug = EnumWire.Slug(format);
        // Order-X is the one route that takes both shapes: a hybrid PDF or the raw CIO XML.
        var acceptsPdf = format is not (ValidationReportFormat.Ubl or ValidationReportFormat.Cii or ValidationReportFormat.XRechnung);
        var acceptsXml = format is not (ValidationReportFormat.FacturX or ValidationReportFormat.Zugferd);

        var rulesError = ExtraRules.Unsupported(rules, slug, "ubl", "cii", "facturx");
        if (rulesError is not null)
            return ArtifactTools.InputError("INPUT-RULES", rulesError, ["rules"], slug);

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

        if (!acceptsPdf && providedPdf)
        {
            return ArtifactTools.InputError("INPUT-SOURCE-MISMATCH",
                $"Format '{slug}' is an XML format; provide the document via xml or xmlUrl, not a PDF input.",
                ["xml", "xmlUrl"], slug);
        }
        if (!acceptsXml && providedXml)
        {
            return ArtifactTools.InputError("INPUT-SOURCE-MISMATCH",
                $"Format '{slug}' is a hybrid PDF; provide the document via pdfBase64 or pdfUrl, not an XML input.",
                ["pdfBase64", "pdfUrl"], slug);
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
            fileName = format is ValidationReportFormat.OrderX ? "order.xml" : "invoice.xml";
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
                    $"Received {pdfBytes.Length:N0} bytes that start like a PDF but have no %%EOF trailer: " +
                    "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                    "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                    ["pdfBase64", "pdfUrl"], slug);
            }

            content = pdfBytes;
            contentType = "application/pdf";
            fileName = format is ValidationReportFormat.OrderX ? "order.pdf" : "invoice.pdf";
        }

        // The verdict rides on the client result, not the artifact, so capture it
        // for the summary while ArtifactTools handles packaging and failures.
        ValidationReportPdfResult? outcome = null;

        return await ArtifactTools.ExecuteAsync(
            slug,
            async () =>
            {
                outcome = await _client
                    .ValidationReportPdfAsync(format, content, contentType, fileName, rules, footerBrand, cancellationToken)
                    .ConfigureAwait(false);
                return outcome.Report;
            },
            artifact =>
            {
                var verdict = outcome!.Valid switch
                {
                    true => "the invoice is VALID",
                    false => "the invoice is NOT compliant",
                    null => "see the report for the verdict",
                };
                return $"Validated the {slug} invoice and produced a PDF compliance report ({artifact.Content.Length:N0} bytes) " +
                       $"as {artifact.FileName}; {verdict}. The PDF is delivered as an embedded resource attachment; " +
                       "refer to it by file name and do not attempt to read its bytes. " +
                       "For the machine-readable findings run validate_xml_invoice or validate_pdf_invoice.";
            },
            cancellationToken).ConfigureAwait(false);
    }
}
