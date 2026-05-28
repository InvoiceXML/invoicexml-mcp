using InvoiceXml.Mcp.Core.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceXml.Mcp.Core.Extensions;

/// <summary>
/// MCP-server registration entry points for <c>InvoiceXml.Mcp.Core</c>.
/// </summary>
public static class McpServerBuilderExtensions
{
    /// <summary>
    /// Human- and agent-facing description advertised in the MCP <c>initialize</c>
    /// response via <c>McpServerOptions.ServerInstructions</c>. Clients render this
    /// as the server's headline description and feed it to the model as orientation
    /// before any tool is called. Lives in Core (not the host) so every deployment
    /// shape advertises the same capabilities.
    /// </summary>
    public const string ServerInstructions =
        "InvoiceXML turns structured invoice data into compliant electronic invoices, checks existing " +
        "invoices against the EN 16931 standard, and converts between formats. It covers UBL (Peppol BIS " +
        "Billing 3.0), UN/CEFACT CII, German XRechnung, and the hybrid PDF formats Factur-X and ZUGFeRD.\n" +
        "\n" +
        "Tools, grouped by what they do:\n" +
        "\n" +
        "Create from scratch\n" +
        "• Create Invoice (create_invoice): generate an invoice in any supported format from a structured " +
        "document. XML formats come back inline; PDF formats come back as a downloadable attachment.\n" +
        "\n" +
        "Validate\n" +
        "• Validate XML Invoice (validate_xml_invoice): check a UBL / CII / XRechnung XML document.\n" +
        "• Validate PDF Invoice (validate_pdf_invoice): check a Factur-X / ZUGFeRD hybrid PDF.\n" +
        "\n" +
        "Render\n" +
        "• Render Invoice to PDF (render_invoice): turn a UBL / CII / XRechnung XML into a human-readable " +
        "PDF preview (visual only, no embedded XML).\n" +
        "\n" +
        "Extract and Embed\n" +
        "• Extract Invoice Data (extract_invoice): pull the structured JSON document or the embedded CII XML " +
        "out of a Factur-X / ZUGFeRD hybrid PDF.\n" +
        "• Embed XML into Hybrid PDF (embed_invoice): combine a PDF and a CII XML into a Factur-X / ZUGFeRD hybrid PDF.\n" +
        "\n" +
        "Convert\n" +
        "• Convert Invoice Format (convert_invoice): deterministic syntax conversion between formats " +
        "(e.g. ubl<->cii, xrechnung->ubl, ubl->facturx, facturx->cii).\n" +
        "\n" +
        "Working with files: prefer passing a public https:// URL (pdfUrl / xmlUrl) and let the server " +
        "fetch the file. Only ever use the real bytes or text of a document. Never reconstruct, guess, or " +
        "synthesize file contents; if you cannot read an uploaded file, ask the user for a URL or to paste it.";

    /// <summary>
    /// The InvoiceXML brand mark as a self-contained <c>data:</c> URI (base64 SVG),
    /// advertised on <c>McpServerOptions.ServerInfo.Icons</c>. A data URI renders
    /// regardless of where the server runs (localhost / tunnel / production) and needs
    /// no extra fetch, which is the approach the MCP icon spec (SEP-973) recommends.
    /// Clients that support server icons show it in their connector list; clients that
    /// don't (currently Claude.ai's custom-connector panel) fall back to a generic icon.
    /// </summary>
    public const string IconSvgDataUri =
        "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIzMzQiIGhlaWdodD0iMzM0IiBmaWxsPSJub25lIj48ZyBjbGlwLXBhdGg9InVybCgjYSkiPjxwYXRoIGZpbGw9IiMxZjQ5NWIiIGQ9Ik02NiA2NWgyMTN2MTkwSDY2eiIvPjxwYXRoIGZpbGw9IiNhZGViYjQiIGQ9Ik0yNzcuODIgMzguODA4IDE4Ni41MjUgNC42MTRjLTkuNDYxLTMuNDg1LTI0Ljg5OC0zLjQ4NS0zNC4zNiAwTDYwLjg3IDM4LjgwOEM0My4yNzUgNDUuNDQ4IDI5IDY2LjAzMSAyOSA4NC43ODh2MTM0LjQ1MWMwIDEzLjQ0NSA4Ljc5OCAzMS4yMDYgMTkuNTg3IDM5LjE3NGw5MS4yOTUgNjguMjIyYzE2LjEwMSAxMi4xMTcgNDIuNDk0IDEyLjExNyA1OC41OTUgMGw5MS4yOTUtNjguMjIyYzEwLjc4OS04LjEzNCAxOS41ODctMjUuNzI5IDE5LjU4Ny0zOS4xNzRWODQuNzg4Yy4xNjYtMTguNzU3LTE0LjExLTM5LjM0LTMxLjUzOS00NS45OG0tMTAuNTEyIDgwLjg0MkwxNTYuNTAyIDIzNi40MTdjLTMuODY1IDQuMDc0LTguNzYxIDUuOTc1LTEzLjY1OCA1Ljk3NS00Ljg5NiAwLTkuNzkyLTEuOTAxLTEzLjY1Ny01Ljk3NWwtNDEuMjMtNDMuOTkyYy03LjQ3NC03Ljg3NS03LjQ3NC0yMC45MDkgMC0yOC43ODVhMTguNzQ0IDE4Ljc0NCAwIDAgMSAyNy4zMTQgMGwyNy44MzEgMjkuMzI4IDk3LjE0OS0xMDIuMzc1YTE4Ljc0MyAxOC43NDMgMCAwIDEgMjcuMzE1IDBjNy40NzMgNy44NzUgNy40NzMgMjEuMTgxLS4yNTggMjkuMDU3Ii8+PC9nPjxkZWZzPjxjbGlwUGF0aCBpZD0iYSI+PHBhdGggZmlsbD0iI2ZmZiIgZD0iTTAgMGgzMzR2MzM0SDB6Ii8+PC9jbGlwUGF0aD48L2RlZnM+PC9zdmc+";

    /// <summary>
    /// Registers every <c>[McpServerToolType]</c> class in this assembly with the
    /// MCP server. Use this from your host's <c>Program.cs</c> right after
    /// <c>AddMcpServer()</c>.
    /// </summary>
    /// <remarks>
    /// Scoped to the Core assembly so adding a new tool is one file change — no
    /// host edits, no DI manifest. This is the OCP seam for the tool layer.
    /// </remarks>
    public static IMcpServerBuilder WithInvoiceXmlTools(this IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithToolsFromAssembly(typeof(CreateInvoiceTool).Assembly);
    }
}
