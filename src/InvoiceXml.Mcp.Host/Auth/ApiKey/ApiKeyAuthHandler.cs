using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Host.Auth.ApiKey;

/// <summary>
/// Attaches the configured static API key as a <c>Bearer</c> token on every outbound
/// request made by <see cref="Core.Interfaces.IInvoiceXmlClient"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="IOptionsMonitor{TOptions}"/> so that re-reading the key after a config
/// reload (e.g. environment change in the orchestrator) does not require a process restart.
/// </remarks>
internal sealed class ApiKeyAuthHandler : DelegatingHandler
{
    private readonly IOptionsMonitor<ApiKeyOptions> _options;

    public ApiKeyAuthHandler(IOptionsMonitor<ApiKeyOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = _options.CurrentValue.Value;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "InvoiceXML API key is not configured. Set Mcp:ApiKey:Value " +
                "(or the INVOICEXML_API_KEY environment variable) before starting the host.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
