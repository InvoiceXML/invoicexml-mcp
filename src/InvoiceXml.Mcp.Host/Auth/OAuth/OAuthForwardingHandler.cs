using Microsoft.AspNetCore.Http;

namespace InvoiceXml.Mcp.Host.Auth.OAuth;

/// <summary>
/// Forwards the inbound MCP request's <c>Authorization: Bearer …</c> header to
/// the outbound InvoiceXML API call, unchanged. This is the OAuth-mode
/// counterpart of <see cref="ApiKey.ApiKeyAuthHandler"/>.
/// </summary>
/// <remarks>
/// We do not validate the token here. The MCP server is a Resource Server in
/// OAuth terms; the InvoiceXML API is the source of truth for whether a token
/// belongs to an active user. If the token is invalid the downstream API
/// returns 401 and we propagate it.
/// </remarks>
internal sealed class OAuthForwardingHandler : DelegatingHandler
{
    private const string BearerPrefix = "Bearer ";

    private readonly IHttpContextAccessor _httpContext;

    public OAuthForwardingHandler(IHttpContextAccessor httpContext)
    {
        _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inboundAuth = _httpContext.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(inboundAuth) ||
            !inboundAuth.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Should have been caught by BearerChallengeMiddleware before reaching here.
            // Fail loudly so misconfiguration surfaces in logs instead of silently
            // calling the API anonymously.
            throw new InvalidOperationException(
                "OAuth mode is active but the inbound MCP request has no Bearer token. " +
                "Check that BearerChallengeMiddleware is registered before MapMcp().");
        }

        // Headers.Authorization on the outbound HttpRequestMessage uses
        // AuthenticationHeaderValue which validates the scheme/parameter shape.
        // TryAddWithoutValidation preserves the inbound string verbatim.
        request.Headers.TryAddWithoutValidation("Authorization", inboundAuth);
        return base.SendAsync(request, cancellationToken);
    }
}
