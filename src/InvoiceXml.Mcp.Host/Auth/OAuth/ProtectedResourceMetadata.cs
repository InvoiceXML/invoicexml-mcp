using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Host.Auth.OAuth;

/// <summary>
/// Shape of the OAuth 2.0 Protected Resource Metadata document
/// (draft-ietf-oauth-resource-metadata), served at
/// <c>/.well-known/oauth-protected-resource</c>.
/// </summary>
/// <remarks>
/// MCP clients fetch this document after receiving a 401 with a
/// <c>WWW-Authenticate: Bearer resource_metadata="…"</c> header. The
/// <c>authorization_servers</c> field tells them where to perform the
/// OAuth dance.
/// </remarks>
public sealed class ProtectedResourceMetadata
{
    /// <summary>The MCP server's own canonical URL — what clients are authorising against.</summary>
    [JsonPropertyName("resource")]
    public required string Resource { get; init; }

    /// <summary>Issuer URLs that this resource accepts tokens from.</summary>
    [JsonPropertyName("authorization_servers")]
    public required IReadOnlyList<string> AuthorizationServers { get; init; }

    /// <summary>Scopes the client may request when authorising for this resource.</summary>
    [JsonPropertyName("scopes_supported")]
    public required IReadOnlyList<string> ScopesSupported { get; init; }

    /// <summary>How clients may send the bearer token; always <c>["header"]</c> for MCP.</summary>
    [JsonPropertyName("bearer_methods_supported")]
    public IReadOnlyList<string> BearerMethodsSupported { get; init; } = ["header"];
}
