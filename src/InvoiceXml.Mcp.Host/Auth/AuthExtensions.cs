using InvoiceXml.Mcp.Host.Auth.ApiKey;
using InvoiceXml.Mcp.Host.Auth.OAuth;
using InvoiceXml.Mcp.Host.Configuration;
using Microsoft.Extensions.Configuration;

namespace InvoiceXml.Mcp.Host.Auth;

/// <summary>
/// Single dispatch point that wires the host's outbound auth based on
/// <see cref="McpHostOptions.AuthMode"/>. Adding a new mode means
/// extending <see cref="AuthMode"/> and adding one arm here, nothing else.
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// Attaches the auth pipeline matching <paramref name="mode"/> to the supplied
    /// <see cref="IHttpClientBuilder"/> (the one returned by
    /// <c>AddInvoiceXmlMcpCore</c>).
    /// </summary>
    public static IHttpClientBuilder AddHostAuth(
        this IHttpClientBuilder builder,
        IConfiguration configuration,
        AuthMode mode)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        return mode switch
        {
            AuthMode.ApiKey => builder.AddApiKeyAuth(configuration),
            AuthMode.OAuth => builder.AddOAuthAuth(configuration),
            _ => throw new InvalidOperationException(
                $"Unknown {nameof(AuthMode)}: {mode}. Update {nameof(AuthExtensions)} to handle it."),
        };
    }
}
