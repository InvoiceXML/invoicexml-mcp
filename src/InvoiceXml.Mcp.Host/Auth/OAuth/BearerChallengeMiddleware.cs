using InvoiceXml.Mcp.Host.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Host.Auth.OAuth;

/// <summary>
/// Issues an OAuth 2.0 <c>WWW-Authenticate</c> challenge when the MCP endpoint
/// is hit without a Bearer token. The <c>resource_metadata</c> parameter points
/// MCP clients at <c>/.well-known/oauth-protected-resource</c> so they can
/// discover the authorization server.
/// </summary>
/// <remarks>
/// The MCP endpoint is mounted at <c>POST /</c> (the welcome page lives at
/// <c>GET /</c>). Gating is therefore method-aware: we challenge only on POSTs
/// to the root. Every other request (the welcome page, the well-known
/// metadata, the health endpoint) is public.
/// </remarks>
internal sealed class BearerChallengeMiddleware
{
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly IOptions<McpDeploymentOptions> _deployment;

    public BearerChallengeMiddleware(RequestDelegate next, IOptions<McpDeploymentOptions> deployment)
    {
        _next = next;
        _deployment = deployment;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (!IsMcpRequest(context.Request))
        {
            return _next(context);
        }

        if (HasBearer(context))
        {
            return _next(context);
        }

        return Challenge(context);
    }

    private static bool IsMcpRequest(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && request.Path == "/";

    private Task Challenge(HttpContext context)
    {
        var resourceMetadataUrl =
            _deployment.Value.McpUri.TrimEnd('/') + "/.well-known/oauth-protected-resource";

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] =
            $"Bearer resource_metadata=\"{resourceMetadataUrl}\"";
        return Task.CompletedTask;
    }

    private static bool HasBearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.Length > BearerPrefix.Length &&
               header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
