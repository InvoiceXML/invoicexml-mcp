using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Options;
using InvoiceXml.Mcp.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Core.Extensions;

/// <summary>
/// DI registration entry points for <c>InvoiceXml.Mcp.Core</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the InvoiceXML typed HTTP client (binding <see cref="InvoiceXmlClientOptions"/>
    /// from the <c>InvoiceXml</c> section), plus the URL-fetch service that backs the
    /// validation tools' URL input mode (binding <see cref="FileInputOptions"/> from
    /// <c>Mcp:FileInput</c>). The returned <see cref="IHttpClientBuilder"/> lets the host
    /// attach authentication via <see cref="DelegatingHandler"/>s without leaking auth concerns
    /// into the SDK.
    /// </summary>
    public static IHttpClientBuilder AddInvoiceXmlMcpCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<InvoiceXmlClientOptions>()
            .Bind(configuration.GetSection(InvoiceXmlClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddFileInputServices(services, configuration);

        return services.AddHttpClient<IInvoiceXmlClient, HttpInvoiceXmlClient>(ConfigureHttpClient);
    }

    private static void AddFileInputServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileInputOptions>()
            .Bind(configuration.GetSection(FileInputOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IRemoteFileFetcher, RemoteFileFetcher>();

        // Dedicated client for URL fetches: NO Authorization handler (must never
        // forward the user's bearer token to third-party URLs), redirects handled
        // manually by the fetcher (so each hop is SSRF-re-validated), and an
        // infinite client timeout because the fetcher controls the deadline itself
        // via a linked CancellationTokenSource that also covers the body read.
        services.AddHttpClient(RemoteFileFetcher.HttpClientName, http =>
        {
            http.Timeout = Timeout.InfiniteTimeSpan;
            http.DefaultRequestHeaders.UserAgent.ParseAdd("InvoiceXML-MCP/1.0 (+https://invoicexml.com)");
        })
        .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler { AllowAutoRedirect = false });
    }

    private static void ConfigureHttpClient(IServiceProvider sp, HttpClient http)
    {
        var options = sp.GetRequiredService<IOptions<InvoiceXmlClientOptions>>().Value;
        http.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        http.Timeout = options.Timeout;
    }
}
