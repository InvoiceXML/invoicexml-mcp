using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceXml.Mcp.Host.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Host.Auth.OAuth;

/// <summary>
/// Maps the <c>GET /.well-known/oauth-protected-resource</c> endpoint that
/// MCP clients fetch to discover the authorization server.
/// </summary>
public static class ProtectedResourceMetadataEndpoint
{
    // snake_case naming on the wire matches the AS's well-known document and
    // RFC convention; isolated here so it doesn't leak into the global options.
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Adds the protected-resource metadata route. Call this in your host's
    /// <c>Program.cs</c> when running in OAuth mode.
    /// </summary>
    public static IEndpointConventionBuilder MapMcpProtectedResourceMetadata(
        this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.MapGet("/.well-known/oauth-protected-resource",
            (IOptions<McpDeploymentOptions> deployment, IOptions<OAuthOptions> oauth) =>
            {
                var document = new ProtectedResourceMetadata
                {
                    Resource = deployment.Value.McpUri.TrimEnd('/'),
                    AuthorizationServers = [oauth.Value.AuthorizationServer.TrimEnd('/')],
                    ScopesSupported = oauth.Value.ScopesSupported,
                };

                return Results.Json(document, WireOptions, contentType: "application/json");
            });
    }
}
