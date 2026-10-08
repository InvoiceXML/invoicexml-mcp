using System.ComponentModel;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that wraps <c>/v1/validate/order-x</c>. The API takes a hybrid
/// Order-X PDF or the raw Cross-Industry Order XML on the same route, so the
/// tool accepts either shape and labels the upload by its type.
/// </summary>
[McpServerToolType]
public sealed class ValidateOrderTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public ValidateOrderTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "validate_order", Title = "Validate Purchase Order", ReadOnly = true, OpenWorld = true, UseStructuredContent = true)]
    [Description(
        "Validate an Order-X purchase order: a hybrid Order-X PDF (the embedded order-x.xml is checked) or a plain " +
        "UN/CEFACT Cross-Industry Order XML. The Order-X profile (BASIC, COMFORT, EXTENDED) is detected from the " +
        "document's guideline identifier and the matching rules are applied; an unknown identifier falls back to " +
        "COMFORT with an advisory warning. Use validate_xml_invoice / validate_pdf_invoice for invoices, not this tool. " +
        "\n\n" +
        "Provide the order via EXACTLY ONE of these inputs:\n" +
        "• pdfUrl or pdfBase64 for an Order-X PDF (prefer pdfUrl; base64 only for small files).\n" +
        "• xmlUrl or xml for a plain Cross-Industry Order XML.\n" +
        "If you set none or more than one, the result is valid=false with an INPUT-… error explaining what to fix.\n" +
        "\n" +
        "Only use the ACTUAL bytes or text of the file. Never reconstruct, guess, or synthesize content; if you " +
        "cannot access the real file, ask the user for a public https:// URL or to paste it.\n" +
        "\n" +
        "The result has a 'valid' field. On valid=false the 'errors' array lists each rule failure; explain these " +
        "to the user and you may suggest corrections. For a printable report use render_validation_report with " +
        "format 'order-x'.")]
    public async Task<ValidationResult> ValidateOrderAsync(
        CancellationToken cancellationToken,

        [Description("A public https:// URL to an Order-X PDF. Provide exactly one input.")]
        string? pdfUrl = null,

        [Description("An Order-X PDF as base64 (small files only). Provide exactly one input.")]
        string? pdfBase64 = null,

        [Description("A public https:// URL to a Cross-Industry Order XML. Provide exactly one input.")]
        string? xmlUrl = null,

        [Description("A Cross-Industry Order XML as plain text. Provide exactly one input.")]
        string? xml = null)
    {
        var exclusive = FileInputResolver.ValidateExactlyOne(
        [
            ("pdfUrl", !string.IsNullOrWhiteSpace(pdfUrl)),
            ("pdfBase64", !string.IsNullOrWhiteSpace(pdfBase64)),
            ("xmlUrl", !string.IsNullOrWhiteSpace(xmlUrl)),
            ("xml", !string.IsNullOrWhiteSpace(xml)),
        ]);
        if (exclusive is not null)
            return exclusive;

        byte[] content;
        string contentType;
        string fileName;

        if (!string.IsNullOrWhiteSpace(xml) || !string.IsNullOrWhiteSpace(xmlUrl))
        {
            string xmlText;
            if (!string.IsNullOrWhiteSpace(xml))
            {
                xmlText = xml;
            }
            else
            {
                var (fetched, error) = await FileInputResolver
                    .FetchUrlAsync(_fetcher, xmlUrl!, "xmlUrl", cancellationToken)
                    .ConfigureAwait(false);
                if (error is not null)
                    return error;
                xmlText = ArtifactTools.DecodeUtf8(fetched!);
            }

            content = System.Text.Encoding.UTF8.GetBytes(xmlText);
            contentType = "application/xml";
            fileName = "order.xml";
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(pdfUrl))
            {
                var (fetched, error) = await FileInputResolver
                    .FetchUrlAsync(_fetcher, pdfUrl, "pdfUrl", cancellationToken)
                    .ConfigureAwait(false);
                if (error is not null)
                    return error;
                content = fetched!;
            }
            else
            {
                try
                {
                    content = Convert.FromBase64String(pdfBase64!);
                }
                catch (FormatException)
                {
                    return FileInputResolver.InputError("INPUT-BASE64",
                        "pdfBase64 is not valid base64. Use standard base64 (no chunking, no URL-safe alphabet). " +
                        "For anything but a small file, pass a public https:// URL via pdfUrl instead.",
                        ["pdfBase64"]);
                }
            }

            if (PdfSniffer.IsIncompletePdf(content))
            {
                return FileInputResolver.InputError("INPUT-INCOMPLETE-PDF",
                    $"Received {content.Length:N0} bytes that start like a PDF but have no %%EOF trailer; " +
                    "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                    "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                    ["pdfBase64", "pdfUrl"]);
            }

            contentType = "application/pdf";
            fileName = "order.pdf";
        }

        try
        {
            return await _client.ValidateOrderAsync(content, contentType, fileName, cancellationToken).ConfigureAwait(false);
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
