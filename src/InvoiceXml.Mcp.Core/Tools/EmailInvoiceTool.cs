using System.ComponentModel;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// MCP tool that wraps the API's <c>/v1/create/{format}/email</c> family: creates
/// the invoice exactly like <c>create_invoice</c> and emails it instead of returning it.
/// </summary>
[McpServerToolType]
public sealed class EmailInvoiceTool
{
    private readonly IInvoiceXmlClient _client;

    public EmailInvoiceTool(IInvoiceXmlClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    [McpServerTool(Name = "email_invoice", Title = "Create and Email Invoice", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description(
        "Create a compliant e-invoice and email it to the recipient from the user's own verified domain, in one call. " +
        "The invoice and options work exactly as in 'create_invoice', including the format choice and the rules " +
        "described there; the file is attached to the email instead of being returned. Formats 'facturx' and " +
        "'zugferd' attach a hybrid PDF, formats 'ubl', 'cii' and 'xrechnung' attach the XML. " +
        ExtraRules.FacturXDefaultGuidance + " On this tool that setting is options.rules. " +
        "\n\n" +
        EmailTools.ConfirmationGuidance +
        "\n\n" +
        EmailTools.OutcomeGuidance)]
    public async Task<CallToolResult> EmailInvoiceAsync(
        [Description("Target e-invoicing format. Must be one of: ubl, cii, xrechnung, facturx, zugferd.")]
        InvoiceFormat format,

        [Description("The invoice document, the same model as 'create_invoice'.")]
        InvoiceDocument invoice,

        [Description("The email: sender, recipient and, optionally, copies, reply-to address, subject and message.")]
        EmailMessage email,

        [Description("Optional creation settings, the same as on 'create_invoice'.")]
        CreateInvoiceOptions? options,

        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(email);

        var slug = format.ToString().ToLowerInvariant();
        var refused = CreateInvoiceTool.RefuseOptions(options, slug);
        if (refused is not null)
            return refused;

        return await EmailTools.SendAsync(
            slug,
            email.To,
            () => _client.EmailInvoiceAsync(format, invoice, options, email, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }
}
