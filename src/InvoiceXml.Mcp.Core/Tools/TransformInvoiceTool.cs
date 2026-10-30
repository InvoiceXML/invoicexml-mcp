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
/// AI data extraction over the PDF, so it also handles a plain (non-hybrid) invoice PDF.
/// XML targets (ubl / cii / xrechnung) return XML; hybrid-PDF targets (facturx /
/// zugferd) return a PDF/A-3 with the XML embedded, its pages rendered from the
/// extracted data or, with <c>preserveOriginalPdf</c>, kept from the upload. Because the
/// data comes out of an AI read, the tool text makes the agent warn the user and offer
/// a preview of what was read (<see cref="AiReviewGuidance"/>), and keeps the rendered
/// pages' "Electronic invoice details" section on (<see cref="AdvancedPropertiesGuidance"/>)
/// so the user can check the embedded XML on the pages.
/// </summary>
[McpServerToolType]
public sealed class TransformInvoiceTool
{
    /// <summary>
    /// What the agent owes the user after a successful transform: the data came out of an
    /// AI extraction, so the user hears that first and is offered a look at what was read,
    /// as a PDF or in the chat, before the invoice goes anywhere. Part of the tool
    /// description; pinned by the schema tests.
    /// </summary>
    public const string AiReviewGuidance =
        "AFTER EVERY SUCCESSFUL CALL, before anything else, tell the user that the invoice data was read from the " +
        "PDF by AI and that they must check it against the source document themselves before the invoice is used " +
        "or sent: amounts, dates, parties, line items and VAT can be misread. Never present the result as verified. " +
        "Then offer a preview of the extracted data and let the user choose:\n" +
        "• As a PDF: for an XML target (ubl / cii / xrechnung) call render_invoice with the same format and the " +
        "returned XML, and hand the user the PDF. For a facturx / zugferd target the returned PDF already shows " +
        "the extracted data on its pages, and with includeAdvancedProperties (on by default) also lists the " +
        "technical properties of the embedded XML in an 'Electronic invoice details' section at the end, so the " +
        "user can check them on the pages too. With preserveOriginalPdf set, the pages are the user's own and " +
        "the extracted data sits only in the embedded XML.\n" +
        "• In the chat: call extract_invoice with target 'json' and present the fields in a readable form (seller, " +
        "buyer, dates, lines, VAT breakdown, totals), pointing out anything that looks off. For an XML target pass " +
        "the returned XML as 'xml'. For a facturx / zugferd target the input is the produced PDF, which you can " +
        "only send once the user gives you a public https:// URL to it; never read the attachment's bytes for this.\n" +
        "Give the warning even when the user declines the preview.";

    /// <summary>
    /// Why the <c>includeAdvancedProperties</c> switch stays on: a transform needs careful human
    /// review, and the section it adds lets the user verify on the visual pages what the
    /// embedded XML carries. Part of the tool description; pinned by the schema tests.
    /// </summary>
    public const string AdvancedPropertiesGuidance =
        "A transform needs careful human review, so LEAVE 'includeAdvancedProperties' ON (the default): the " +
        "'Electronic invoice details' section it adds to the rendered pages lets the user verify the technical " +
        "properties of the embedded XML (identifiers, electronic addresses, line references) without opening " +
        "the XML. Turn it off only when the user explicitly asks for a classic layout without that section. It " +
        "has no effect with preserveOriginalPdf, which keeps the uploaded pages as they are.";

    /// <summary>The reminder every success summary ends with, so the warning is not left to the description alone.</summary>
    public const string ReviewReminder =
        "The invoice data was read by AI: tell the user to check it against the source PDF before the invoice is " +
        "used or sent, and offer a preview of what was read (as a PDF, or in the chat).";

    private readonly IInvoiceXmlClient _client;
    private readonly IRemoteFileFetcher _fetcher;

    public TransformInvoiceTool(IInvoiceXmlClient client, IRemoteFileFetcher fetcher)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
    }

    [McpServerTool(Name = "transform_invoice", Title = "Transform PDF to E-Invoice", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description(
        "Turn a PDF invoice into a structured EN 16931 e-invoice by reading its data with AI and rebuilding it. " +
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
        "• pdfUrl: a public https:// URL to the PDF; the server downloads it. PREFER THIS whenever a URL exists.\n" +
        "• pdfBase64: the PDF as base64. Only practical for small files; larger base64 gets corrupted in a tool call.\n" +
        "If you set neither or both, the result is an input error explaining what to fix.\n" +
        "\n" +
        "For a facturx / zugferd target, 'language' sets the language of the rendered PDF pages and of the default " +
        "texts in the XML (EN, DE or FR; leave it out to follow the language the invoice is written in, English when " +
        "that is none of the three), 'preserveOriginalPdf' keeps the uploaded PDF as the pages and only embeds " +
        "the XML built from it, and 'includeAdvancedProperties' adds an 'Electronic invoice details' section to " +
        "the rendered pages that lists the technical properties of the embedded XML. For an xrechnung target, " +
        "'buyerReference' supplies the BT-10 buyer reference (Leitweg-ID) XRechnung requires.\n" +
        "\n" +
        AdvancedPropertiesGuidance + "\n" +
        "\n" +
        "Only use the ACTUAL bytes of the file. Never reconstruct, guess, or synthesize a PDF. " +
        "If you cannot access the real file, ask the user for a public https:// URL (use pdfUrl) or to paste its base64.\n" +
        "\n" +
        "On success the result is a short summary plus the document: XML targets inline as text, " +
        "PDF (facturx/zugferd) targets as an embedded resource attachment. On failure the result has isError=true " +
        "and a JSON body with { success:false, failureCategory, errors[], guidance, invoiceData? }.\n" +
        "\n" +
        AiReviewGuidance + "\n" +
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

        [Description(
            "For a facturx / zugferd target: language of the rendered PDF pages and of the default texts in the " +
            "embedded XML. EN, DE or FR; leave it out for the language the invoice is written in (English when " +
            "that is none of the three).")]
        PdfLanguage? language = null,

        [Description(
            "For a facturx / zugferd target: keep the uploaded PDF as the visual pages and only embed the XML " +
            "built from it; earlier attachments and form fields are not kept. Default false: the pages are " +
            "rendered fresh from the extracted data.")]
        bool preserveOriginalPdf = false,

        [Description(
            "For a facturx / zugferd target: add an 'Electronic invoice details' section at the end of the rendered " +
            "pages that lists the technical properties the embedded XML carries but a classic invoice layout does " +
            "not show (specification and business process identifiers, party identifiers with their schemes, " +
            "electronic addresses, line-level references), so the user can verify those values on the pages. " +
            "Default true; leave it on, a transform needs careful human review. Set " +
            "false only when the user explicitly asks for a classic layout without that section. No effect with " +
            "preserveOriginalPdf.")]
        bool includeAdvancedProperties = true,

        [Description("For an xrechnung target: the BT-10 buyer reference (Leitweg-ID) required by XRechnung.")]
        string? buyerReference = null,

        [Description(ValidationLanguages.ParameterDescription + " Applies to the findings returned when the rebuilt invoice fails validation.")]
        ValidationLanguage? validationLanguage = null)
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
            () => _client.TransformAsync(targetFormat, bytes!, language, preserveOriginalPdf, includeAdvancedProperties, buyerReference, validationLanguage, cancellationToken),
            artifact => targetIsPdf
                ? $"Transformed the source PDF into a {slug} invoice ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. " +
                  "Delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes. " +
                  (preserveOriginalPdf
                      ? "The pages are the uploaded PDF's own; the extracted data is in the embedded XML only. "
                      : includeAdvancedProperties
                          ? "Its pages are rendered from the extracted data and end with an 'Electronic invoice details' section " +
                            "listing the technical properties of the embedded XML, so the file doubles as the PDF preview. "
                          : "Its pages are rendered from the extracted data, so the file doubles as the PDF preview. ") +
                  ReviewReminder
                : $"Transformed the source PDF into a {slug} invoice ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. " +
                  $"The {slug} XML is included inline below. " + ReviewReminder,
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
                $"Received {bytes.Length:N0} bytes that start like a PDF but have no %%EOF trailer: " +
                "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                ["pdfBase64", "pdfUrl"], slug));
        }

        return (bytes, null);
    }
}
