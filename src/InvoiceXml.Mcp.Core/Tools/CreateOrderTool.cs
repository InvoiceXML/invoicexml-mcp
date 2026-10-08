using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that wraps the API's Order-X create family:
/// <c>/v1/create/cio</c> (plain Cross-Industry Order XML) and
/// <c>/v1/create/order-x</c> (hybrid PDF/A-3). Orders reuse the invoice
/// document model, so the agent fills the same schema it knows from
/// <c>create_invoice</c>.
/// </summary>
[McpServerToolType]
public sealed class CreateOrderTool
{
    private readonly IInvoiceXmlClient _client;

    public CreateOrderTool(IInvoiceXmlClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    [McpServerTool(Name = "create_order", Title = "Create Purchase Order", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description(
        "Generate an Order-X purchase order (the order counterpart of Factur-X, a UN/CEFACT Cross-Industry Order) " +
        "from a structured document. Choose 'format': 'order-x' for a hybrid PDF/A-3 (readable order with the " +
        "XML embedded), or 'cio' for the plain Cross-Industry Order XML. " +
        "\n\n" +
        "The order uses the same document model as create_invoice: invoiceNumber carries the ORDER number, " +
        "seller is the supplier, buyer is the party placing the order, and lines, totals and vatBreakdowns " +
        "describe what is ordered. Set options.typeCode for an order change ('230') or an order response ('231'), " +
        "and options.profile for the Order-X profile ('basic', 'comfort' by default, or 'extended'). " +
        "\n\n" +
        "On success the result contains a short summary plus the generated document: the 'cio' XML inline as text, " +
        "the 'order-x' PDF as an embedded resource attachment (refer to it by file name and do not attempt to read " +
        "its bytes). On failure the result has isError=true and a JSON body " +
        "{ success: false, failureCategory, statusCode, errors[], guidance, invoiceData? }. If failureCategory is " +
        "'Validation', fix the fields named in errors on the echoed invoiceData and call this tool again; for " +
        "'Unauthorized', 'Forbidden', 'Network' or 'Client' do not retry, surface the failure to the user.")]
    public Task<CallToolResult> CreateOrderAsync(
        [Description("Output format: 'order-x' (hybrid PDF) or 'cio' (plain XML).")]
        OrderFormat format,

        [Description(
            "The order document, in the same EN 16931 BT-first model as create_invoice. At minimum supply " +
            "invoiceNumber (the order number), currency, seller (supplier), buyer and lines.")]
        InvoiceDocument order,

        [Description(
            "Optional settings: 'profile' and 'typeCode' (both formats), and the PDF settings 'language', " +
            "'brandColor', 'logoUrl', 'pdfUrl' and 'footerBrand' (format 'order-x' only). Every field is optional.")]
        CreateOrderOptions? options,

        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        var slug = EnumWire.Slug(format);

        return ArtifactTools.ExecuteAsync(
            slug,
            () => _client.CreateOrderAsync(format, order, options, cancellationToken),
            artifact => format is OrderFormat.OrderX
                ? $"Created an Order-X hybrid PDF ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. " +
                  "The PDF is delivered as an embedded resource attachment; refer to it by file name and do not attempt to read its bytes."
                : $"Created a Cross-Industry Order XML ({artifact.Content.Length:N0} bytes) as {artifact.FileName}. The XML is included inline below.",
            cancellationToken);
    }
}
