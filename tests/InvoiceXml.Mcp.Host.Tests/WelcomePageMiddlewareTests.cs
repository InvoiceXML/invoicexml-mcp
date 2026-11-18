using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using InvoiceXml.Mcp.Core.Tools;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Host.Tests;

public class WelcomePageMiddlewareTests
{
    [Fact]
    public async Task GetRoot_ServesHtmlWithoutCallingNext()
    {
        var called = false;
        var (ctx, body) = await InvokeAsync(HttpMethods.Get, "/", () => called = true);

        Assert.False(called, "The welcome page must short-circuit a GET to the root.");
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.StartsWith("text/html", ctx.Response.ContentType);
        Assert.Contains("InvoiceXML MCP Server", body);
    }

    [Fact]
    public async Task GetRoot_ReferencesOnlyStaticFilesThatExist()
    {
        var (_, body) = await InvokeAsync(HttpMethods.Get, "/", () => { });

        var paths = Regex.Matches(body, "(?:href|src)=\"(/(?:favicon|img)/[^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Contains("/favicon/favicon.svg", paths);
        Assert.Contains("/img/logo.svg", paths);
        Assert.All(paths, path => Assert.True(
            File.Exists(Path.Combine(HostWebRoot(), path.TrimStart('/'))),
            $"The welcome page references {path}, which is not in the host's wwwroot."));
    }

    [Fact]
    public async Task GetRoot_ListsEveryTool()
    {
        var (_, body) = await InvokeAsync(HttpMethods.Get, "/", () => { });

        var toolNames = typeof(CreateInvoiceTool).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .ToList();

        Assert.NotEmpty(toolNames);
        Assert.All(toolNames, name => Assert.Contains($"<code>{name}</code>", body));
    }

    [Fact]
    public async Task PostToRoot_PassesThrough()
    {
        var called = false;
        await InvokeAsync(HttpMethods.Post, "/", () => called = true);

        Assert.True(called, "A POST to the root is the MCP endpoint and must reach it.");
    }

    private static async Task<(HttpContext Context, string Body)> InvokeAsync(
        string method, string path, Action onNext)
    {
        var middleware = new WelcomePageMiddleware(_ => { onNext(); return Task.CompletedTask; });

        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("mcp.invoicexml.test");
        ctx.Request.Path = path;
        using var stream = new MemoryStream();
        ctx.Response.Body = stream;

        await middleware.InvokeAsync(ctx);

        return (ctx, Encoding.UTF8.GetString(stream.ToArray()));
    }

    /// <summary>The host project's wwwroot, found by walking up from the test output folder.</summary>
    private static string HostWebRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "InvoiceXml.Mcp.Host", "wwwroot");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("src/InvoiceXml.Mcp.Host/wwwroot was not found above the test output folder.");
    }
}
