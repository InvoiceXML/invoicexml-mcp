using System.ComponentModel.DataAnnotations;

namespace InvoiceXml.Mcp.Host.Configuration;

/// <summary>
/// Deployment-level host settings that live at the configuration root, separate
/// from <see cref="McpHostOptions"/>. Used by OAuth-mode wiring to advertise the
/// MCP server's public URL in its resource metadata document.
/// </summary>
public sealed class McpDeploymentOptions
{
    /// <summary>
    /// Public origin where this MCP server is reachable, e.g.
    /// <c>https://mcp.invoicexml.com</c>. Required when running in OAuth mode
    /// because it appears in the protected-resource metadata that MCP clients
    /// fetch during the authorization dance. Trailing slash is ignored.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string McpUri { get; set; } = string.Empty;
}
