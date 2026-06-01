using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that wraps the API's <c>/v1/create/{format}</c> family.
/// </summary>
/// <remarks>
/// The success path delegates to <see cref="ToolResults.ForArtifact"/>, which
/// puts PDF outputs into an <see cref="EmbeddedResourceBlock"/> so MCP clients
/// render them as downloadable attachments instead of feeding the base64 into
/// the LLM context window. Failures go through <see cref="ToolResults.ForFailure{TPayload}"/>
/// which sets <see cref="CallToolResult.IsError"/> so agent frameworks can apply
/// retry policy. Neither concern is implemented in this class; both seams live
/// in <see cref="ToolResults"/> so future <c>convert_*</c> / <c>render_*</c>
/// tools can reuse them without duplication.
/// </remarks>
[McpServerToolType]
public sealed class CreateInvoiceTool
{
    private readonly IInvoiceXmlClient _client;

    public CreateInvoiceTool(IInvoiceXmlClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    [McpServerTool(Name = "create_invoice", Title = "Create Invoice", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description(
        "Generate a compliant e-invoice from a structured invoice document. " +
        "Choose the format that matches the buyer's jurisdiction: " +
        "'ubl' for Peppol / international Peppol BIS Billing 3.0, " +
        "'cii' for generic UN/CEFACT CII XML, " +
        "'xrechnung' for German public sector, " +
        "'facturx' for French and German private sector (hybrid PDF), " +
        "'zugferd' for German private sector (hybrid PDF). " +
        "\n\n" +
        "On success the tool result contains a short summary plus the generated artefact. " +
        "XML formats include the XML text inline as a second text block (you may quote or explain it). " +
        "PDF formats deliver the file as an embedded resource attachment alongside the summary; " +
        "DO NOT attempt to read or quote the PDF bytes — refer to the file by its name in your response to the user. " +
        "\n\n" +
        "On failure the result has isError=true and a single JSON content block with " +
        "{ success: false, failureCategory, statusCode, errors[], guidance, invoiceData? }. " +
        "If failureCategory is 'Validation', the errors array tells you which fields to fix " +
        "(each entry has 'message', EN 16931 'btCodes', and JSON 'fields' paths), and 'invoiceData' echoes back " +
        "the document the API parsed; correct the named fields on that 'invoiceData' object and call this tool again with it. " +
        "If failureCategory is 'Unauthorized', 'Forbidden', 'Network', or 'Client', do not retry; surface the failure to the user.")]
    public async Task<CallToolResult> CreateInvoiceAsync(
        [Description("Target e-invoicing format. Must be one of: ubl, cii, xrechnung, facturx, zugferd.")]
        InvoiceFormat format,

        [Description("The invoice document. EN 16931 BT-first model: at minimum supply invoiceNumber, currency, seller, buyer, lines, totals and vatBreakdowns.")]
        InvoiceDocument invoice,

        [Description("Optional PDF render settings. Ignored for XML formats. Defaults to English face with no brand colour.")]
        PdfRenderOptions? options,

        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        try
        {
            var result = await _client.CreateInvoiceAsync(format, invoice, options, cancellationToken)
                .ConfigureAwait(false);

            return ToolResults.ForArtifact(
                summary: BuildSuccessSummary(format, result),
                content: result.Content,
                contentType: result.ContentType,
                fileName: result.FileName);
        }
        catch (InvoiceXmlApiException ex)
        {
            return ToolResults.ForFailure(BuildFailurePayload(format, ToolFailure.FromApiException(ex)));
        }
        catch (HttpRequestException ex)
        {
            return ToolResults.ForFailure(BuildFailurePayload(format, ToolFailure.FromNetworkException(ex)));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient timeouts surface as TaskCanceledException with no caller cancellation —
            // treat as a network-class failure rather than letting it propagate as a generic crash.
            return ToolResults.ForFailure(BuildFailurePayload(format, ToolFailure.FromNetworkException(ex)));
        }
    }

    private static string BuildSuccessSummary(InvoiceFormat format, CreateInvoiceResult result)
    {
        var formatSlug = format.ToString().ToLowerInvariant();
        var isPdf = !IsTextContentType(result.ContentType);

        return isPdf
            ? $"Created {formatSlug} hybrid PDF ({result.Content.Length:N0} bytes) as {result.FileName}. " +
              "The PDF is delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes."
            : $"Created {formatSlug} XML ({result.Content.Length:N0} bytes) as {result.FileName}.";
    }

    private static ToolFailurePayload BuildFailurePayload(InvoiceFormat format, ToolFailure.Translation t) =>
        new()
        {
            Success = false,
            Summary = t.Summary,
            Format = format.ToString().ToLowerInvariant(),
            FailureCategory = t.Category,
            StatusCode = t.StatusCode,
            Errors = t.Errors,
            Guidance = t.Guidance,
            InvoiceData = t.Problem?.InvoiceData,
        };

    private static bool IsTextContentType(string contentType) =>
        contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase) ||
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
}
