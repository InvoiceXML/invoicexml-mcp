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
}
