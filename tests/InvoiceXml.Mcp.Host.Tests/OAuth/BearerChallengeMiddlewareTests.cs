using System.Text;
using InvoiceXml.Mcp.Host.Auth.OAuth;
using InvoiceXml.Mcp.Host.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Host.Tests.OAuth;

public class BearerChallengeMiddlewareTests
{
    private static BearerChallengeMiddleware Build(RequestDelegate next) =>
        new(next, Options.Create(new McpDeploymentOptions { McpUri = "https://mcp.invoicexml.test" }));

    [Fact]
    public async Task PostToRoot_WithoutBearer_Returns401AndChallengeHeader()
    {
        var called = false;
        var middleware = Build(_ => { called = true; return Task.CompletedTask; });

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Request.Path = "/";

        await middleware.InvokeAsync(ctx);

        Assert.False(called, "Middleware must short-circuit when no Bearer is present.");
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);

        var challenge = ctx.Response.Headers["WWW-Authenticate"].ToString();
        Assert.StartsWith("Bearer ", challenge);
        Assert.Contains(
            "resource_metadata=\"https://mcp.invoicexml.test/.well-known/oauth-protected-resource\"",
            challenge);
    }

    [Fact]
    public async Task PostToRoot_WithBearer_PassesThrough()
    {
        var called = false;
        var middleware = Build(_ => { called = true; return Task.CompletedTask; });

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Request.Path = "/";
        ctx.Request.Headers.Authorization = "Bearer some-token-value";

        await middleware.InvokeAsync(ctx);

        Assert.True(called);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task GetRoot_AlwaysPassesThrough()
    {
        // GET / is the welcome page — must reach the next middleware regardless of auth.
        var called = false;
        var middleware = Build(_ => { called = true; return Task.CompletedTask; });

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Path = "/";

        await middleware.InvokeAsync(ctx);

        Assert.True(called);
    }

    [Fact]
    public async Task HealthRoute_AlwaysPassesThrough()
    {
        var called = false;
        var middleware = Build(_ => { called = true; return Task.CompletedTask; });

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Path = "/health";

        await middleware.InvokeAsync(ctx);

        Assert.True(called);
    }

    [Fact]
    public async Task WellKnownRoute_AlwaysPassesThrough()
    {
        var called = false;
        var middleware = Build(_ => { called = true; return Task.CompletedTask; });

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Path = "/.well-known/oauth-protected-resource";

        await middleware.InvokeAsync(ctx);

        Assert.True(called);
    }

    [Fact]
    public async Task EmptyBearer_TreatedAsMissing()
    {
        var called = false;
        var middleware = Build(_ => { called = true; return Task.CompletedTask; });

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Request.Path = "/";
        ctx.Request.Headers.Authorization = "Bearer ";

        await middleware.InvokeAsync(ctx);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}""")]
    [InlineData("""{"jsonrpc":"2.0","method":"notifications/initialized"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":2,"method":"ping"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":3,"method":"tools/list"}""")]
    public async Task DiscoveryCall_WithoutBearer_PassesThroughWithTheBodyIntact(string message)
    {
        string? bodySeenByEndpoint = null;
        var middleware = Build(async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            bodySeenByEndpoint = await reader.ReadToEndAsync();
        });

        var ctx = PostWithoutBearer(message);
        await middleware.InvokeAsync(ctx);

        Assert.Equal(message, bodySeenByEndpoint);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_account","arguments":{}}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"resources/list"}""")]
    // The method is case-sensitive, as it is for the MCP endpoint.
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"Tools/List"}""")]
    // A batch could carry a tool call behind a discovery call.
    [InlineData("""[{"jsonrpc":"2.0","id":1,"method":"tools/list"}]""")]
    // Two methods in one message: the check and the endpoint could disagree on which one counts.
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","method":"tools/list"}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":7}""")]
    [InlineData("""{"jsonrpc":"2.0","id":1,"result":{}}""")]
    [InlineData("not json")]
    public async Task AnyOtherCall_WithoutBearer_Returns401(string message)
    {
        var called = false;
        var middleware = Build(_ => { called = true; return Task.CompletedTask; });

        var ctx = PostWithoutBearer(message);
        await middleware.InvokeAsync(ctx);

        Assert.False(called, "Only the discovery calls may reach the MCP endpoint without a Bearer.");
        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
        Assert.StartsWith("Bearer ", ctx.Response.Headers["WWW-Authenticate"].ToString());
    }

    private static DefaultHttpContext PostWithoutBearer(string message)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Post;
        ctx.Request.Path = "/";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(message));
        return ctx;
    }
}
