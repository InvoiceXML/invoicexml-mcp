using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that validates hybrid PDF/A-3 e-invoices (Factur-X, ZUGFeRD) via
/// <c>/v1/validate/{format}</c>. The tool extracts the embedded XML on the
/// server side and runs it through the EN 16931 pipeline.
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
        "• pdfUrl — a public https:// URL to the PDF; the server downloads it. PREFER THIS whenever a URL exists.\n" +
        "• pdfBase64 — the PDF as base64. Only practical for small files (a few tens of KB); larger base64 gets " +
        "corrupted when written into a tool call, so use a URL instead.\n" +
        "If you set neither or both, the result is valid=false with an INPUT-… error explaining what to fix.\n" +
        "\n" +
        "Only use the ACTUAL bytes of the file. Never reconstruct, guess, or synthesize a PDF. " +
        "If you cannot access the real file (e.g. a user uploaded it and you can't read its bytes), do NOT call " +
        "this tool with made-up content — ask the user for a public https:// URL (use pdfUrl) or to paste the file's base64.\n" +
        "\n" +
        "The result has a 'valid' field. On valid=true the embedded invoice is compliant ('warnings' may carry " +
        "non-blocking issues). On valid=false the 'errors' array explains what was wrong (e.g. PDF-EMBED when no " +
        "XML is embedded, or specific EN 16931 rule failures). Surface these to the user.")]
    public async Task<ValidationResult> ValidatePdfAsync(
        [Description("Validation profile. Must be one of: facturx, zugferd.")]
        PdfInvoiceFormat format,

        CancellationToken cancellationToken,

        [Description("A public https:// URL to the PDF; the server fetches it. Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfUrl = null,

        [Description("The PDF as base64 (small files only). Provide exactly one of pdfUrl / pdfBase64.")]
        string? pdfBase64 = null)
    {
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
        // payloads are passed straight through — the API gives the better error there.
        if (PdfSniffer.IsIncompletePdf(bytes))
        {
            return FileInputResolver.InputError("INPUT-INCOMPLETE-PDF",
                $"Received {bytes.Length:N0} bytes that start like a PDF but have no %%EOF trailer — " +
                "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                ["pdfBase64", "pdfUrl"]);
        }

        try
        {
            return await _client.ValidatePdfAsync(format, bytes, cancellationToken).ConfigureAwait(false);
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
