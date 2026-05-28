using System.ComponentModel.DataAnnotations;

namespace InvoiceXml.Mcp.Host.Auth.OAuth;

/// <summary>
/// Settings for OAuth 2.1 + Dynamic Client Registration mode. The MCP server
/// acts as a Resource Server only: it never validates tokens locally, it
/// forwards them verbatim to the InvoiceXML API which IS the source of truth
/// for whether a given Bearer token belongs to an active user.
/// </summary>
/// <remarks>
/// Bound from the <c>Mcp:OAuth</c> configuration section.
/// </remarks>
public sealed class OAuthOptions
{
    /// <summary>Configuration section name: <c>Mcp:OAuth</c>.</summary>
    public const string SectionName = "Mcp:OAuth";

    /// <summary>
    /// Issuer base URL of the InvoiceXML authorization server, e.g.
    /// <c>https://invoicexml.com</c>. MCP clients discover the authorize /
    /// token / registration endpoints from this issuer's well-known document.
    /// Trailing slash is ignored.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string AuthorizationServer { get; set; } = string.Empty;

    /// <summary>
    /// Scopes the MCP server advertises in its protected-resource metadata.
    /// Must be a subset of what the AS at <see cref="AuthorizationServer"/>
    /// actually issues. No default is set on the type because the .NET options
    /// binder <em>appends</em> bound values to a list with a default, which would
    /// silently duplicate entries; supply this via appsettings instead.
    /// </summary>
    public IReadOnlyList<string> ScopesSupported { get; set; } = [];
}
