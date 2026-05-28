using InvoiceXml.Mcp.Host.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceXml.Mcp.Host.Auth.OAuth;

/// <summary>
/// Registers OAuth-mode authentication on an existing <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class OAuthAuthExtensions
{
    /// <summary>
    /// Binds <see cref="OAuthOptions"/> and <see cref="McpDeploymentOptions"/>,
    /// registers the <see cref="OAuthForwardingHandler"/>, and attaches it to
    /// the InvoiceXML typed HTTP client so every outbound call carries the
    /// inbound MCP request's Bearer token verbatim.
    /// </summary>
    public static IHttpClientBuilder AddOAuthAuth(
        this IHttpClientBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        // OAuth-mode wiring needs access to the inbound request to read its
        // Authorization header. AddHttpContextAccessor is idempotent.
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddOptions<OAuthOptions>()
            .Bind(configuration.GetSection(OAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.ScopesSupported.Count > 0,
                "Mcp:OAuth:ScopesSupported must list at least one scope.")
            .ValidateOnStart();

        // McpUri lives at the configuration root, not under Mcp:*, because it's
        // a deployment-level concern shared by future host-side features beyond OAuth.
        builder.Services.AddOptions<McpDeploymentOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddTransient<OAuthForwardingHandler>();
        return builder.AddHttpMessageHandler<OAuthForwardingHandler>();
    }
}
