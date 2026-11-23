using System.Text.Json;
using InvoiceXml.Mcp.Host.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Host.Auth.OAuth;

/// <summary>
/// Issues an OAuth 2.0 <c>WWW-Authenticate</c> challenge when the MCP endpoint
/// is asked to do work without a Bearer token. The <c>resource_metadata</c>
/// parameter points MCP clients at <c>/.well-known/oauth-protected-resource</c>
/// so they can discover the authorization server.
/// </summary>
/// <remarks>
/// <para>
/// The MCP endpoint is mounted at <c>POST /</c> (the welcome page lives at
/// <c>GET /</c>). Gating is therefore method-aware: we challenge only on POSTs
/// to the root. Every other request (the welcome page, the well-known
/// metadata, the health endpoint) is public.
/// </para>
/// <para>
/// The discovery calls (connect, list the tools) are public too, so a client or
/// a registry can see what the server offers before the user signs in. They
/// reach no user data and never call the InvoiceXML API. Every other call,
/// <c>tools/call</c> above all, needs the token.
/// </para>
/// </remarks>
internal sealed class BearerChallengeMiddleware
{
    private const string BearerPrefix = "Bearer ";

    private static readonly HashSet<string> DiscoveryMethods = new(StringComparer.Ordinal)
    {
        RequestMethods.Initialize,
        NotificationMethods.InitializedNotification,
        RequestMethods.Ping,
        RequestMethods.ToolsList,
    };

    // A message that names its method twice is refused, so the method checked
    // here is always the one the MCP endpoint goes on to run.
    private static readonly JsonDocumentOptions MessageParsing = new() { AllowDuplicateProperties = false };

    private readonly RequestDelegate _next;
    private readonly IOptions<McpDeploymentOptions> _deployment;

    public BearerChallengeMiddleware(RequestDelegate next, IOptions<McpDeploymentOptions> deployment)
    {
        _next = next;
        _deployment = deployment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsMcpRequest(context.Request) ||
            HasBearer(context) ||
            await IsDiscoveryCallAsync(context.Request, context.RequestAborted).ConfigureAwait(false))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        Challenge(context);
    }

    private static bool IsMcpRequest(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && request.Path == "/";

    /// <summary>
    /// Reads the JSON-RPC method of the request and rewinds the body, so the MCP
    /// endpoint reads the message again from the start.
    /// </summary>
    private static async Task<bool> IsDiscoveryCallAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        try
        {
            using var message = await JsonDocument
                .ParseAsync(request.Body, MessageParsing, cancellationToken)
                .ConfigureAwait(false);

            return message.RootElement.ValueKind == JsonValueKind.Object &&
                   message.RootElement.TryGetProperty("method", out var method) &&
                   method.ValueKind == JsonValueKind.String &&
                   DiscoveryMethods.Contains(method.GetString()!);
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private void Challenge(HttpContext context)
    {
        var resourceMetadataUrl =
            _deployment.Value.McpUri.TrimEnd('/') + "/.well-known/oauth-protected-resource";

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] =
            $"Bearer resource_metadata=\"{resourceMetadataUrl}\"";
    }

    private static bool HasBearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.Length > BearerPrefix.Length &&
               header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
