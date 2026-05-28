using System.ComponentModel;
using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that validates plain-XML invoices (UBL, CII, XRechnung) against the
/// EN 16931 XSD and Schematron layers via <c>/v1/validate/{format}</c>.
/// </summary>
[McpServerToolType]
public sealed class ValidateXmlInvoiceTool
{
    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public ValidateXmlInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "validate_xml_invoice", Title = "Validate XML Invoice", ReadOnly = true, OpenWorld = true, UseStructuredContent = true)]
    [Description(
        "Validate a plain-XML e-invoice against the EN 16931 XSD and Schematron rules. " +
        "Pick the matching 'format': 'ubl' for UBL 2.1 / Peppol BIS 3.0, 'cii' for UN/CEFACT CII, " +
        "'xrechnung' for German XRechnung (CIUS-XR). " +
        "Use THIS tool for plain XML; for Factur-X / ZUGFeRD hybrid PDFs use 'validate_pdf_invoice'. " +
        "\n\n" +
        "Provide the XML via EXACTLY ONE of these inputs:\n" +
        "• xml — the XML document as text. Good for documents that fit comfortably in one tool call.\n" +
        "• xmlUrl — a public https:// URL to the XML; the server downloads it. PREFER THIS for large documents " +
        "(long inline XML can get corrupted when written into a tool call).\n" +
        "If you set neither or both, the result is valid=false with an INPUT-… error explaining what to fix.\n" +
        "\n" +
        "Only use the ACTUAL text of the file. Never reconstruct, guess, or synthesize invoice XML. " +
        "If you cannot access the real file (e.g. a user uploaded it and you can't read its contents), do NOT call " +
        "this tool with made-up XML — ask the user for a public https:// URL (use xmlUrl) or to paste the document.\n" +
        "\n" +
        "The result has a 'valid' field. On valid=true the invoice passed every layer ('warnings' may still carry " +
        "non-blocking issues). On valid=false the 'errors' array lists each rule failure with its EN 16931 BT codes; " +
        "explain these to the user and you may suggest corrections.")]
    public async Task<ValidationResult> ValidateXmlAsync(
        [Description("Validation profile. Must be one of: ubl, cii, xrechnung.")]
        XmlInvoiceFormat format,

        CancellationToken cancellationToken,

        [Description("The invoice XML as plain text. Provide exactly one of xml / xmlUrl.")]
        string? xml = null,

        [Description("A public https:// URL to the XML; the server fetches it. Provide exactly one of xml / xmlUrl.")]
        string? xmlUrl = null)
    {
        var exclusive = FileInputResolver.ValidateExactlyOne(
        [
            ("xml", !string.IsNullOrWhiteSpace(xml)),
            ("xmlUrl", !string.IsNullOrWhiteSpace(xmlUrl)),
        ]);
        if (exclusive is not null)
            return exclusive;

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
            xmlText = DecodeUtf8(fetched!);
        }

        try
        {
            return await _client.ValidateXmlAsync(format, xmlText, cancellationToken).ConfigureAwait(false);
        }
        catch (InvoiceXmlApiException ex)
        {
            return SynthesizeFailureResult(ToolFailure.FromApiException(ex));
        }
        catch (HttpRequestException ex)
        {
            return SynthesizeFailureResult(ToolFailure.FromNetworkException(ex));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return SynthesizeFailureResult(ToolFailure.FromNetworkException(ex));
        }
    }

    // Decodes downloaded bytes as UTF-8, stripping a leading BOM if present so the
    // API's XML parser doesn't choke on it.
    private static string DecodeUtf8(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
    }

    // When the API call itself fails (auth, server, network), we still want the
    // LLM to see the ValidationResult shape it normally consumes. Synthesize one
    // with valid=false and a single synthetic finding describing the API failure.
    internal static ValidationResult SynthesizeFailureResult(ToolFailure.Translation t)
    {
        return new ValidationResult
        {
            Valid = false,
            Detail = t.Summary + " " + t.Guidance,
            Errors = t.Errors ?? [BuildSyntheticFinding(t)],
            Warnings = [],
        };
    }

    private static ValidationFinding BuildSyntheticFinding(ToolFailure.Translation t) => new()
    {
        Rule = t.Category.ToString().ToUpperInvariant(),
        Message = t.Summary,
    };
}
