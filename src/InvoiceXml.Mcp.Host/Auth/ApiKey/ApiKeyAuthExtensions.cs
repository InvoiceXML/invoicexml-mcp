using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceXml.Mcp.Host.Auth.ApiKey;

/// <summary>
/// Registers static-API-key authentication on an existing <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class ApiKeyAuthExtensions
{
    /// <summary>
    /// Binds <see cref="ApiKeyOptions"/>, registers <see cref="ApiKeyAuthHandler"/>,
    /// and attaches it to the InvoiceXML typed HTTP client so every outbound request
    /// carries the configured Bearer token.
    /// </summary>
    public static IHttpClientBuilder AddApiKeyAuth(
        this IHttpClientBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        builder.Services.AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddTransient<ApiKeyAuthHandler>();
        return builder.AddHttpMessageHandler<ApiKeyAuthHandler>();
    }
}
