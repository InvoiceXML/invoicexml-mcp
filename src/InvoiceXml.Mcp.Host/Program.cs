using InvoiceXml.Mcp.Core.Extensions;
using InvoiceXml.Mcp.Host;
using InvoiceXml.Mcp.Host.Auth;
using InvoiceXml.Mcp.Host.Auth.OAuth;
using InvoiceXml.Mcp.Host.Configuration;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

// Friendly env-var alias: INVOICEXML_API_KEY maps to the formal Mcp:ApiKey:Value key
// so deployers don't have to learn the dunder spelling for one variable.
if (Environment.GetEnvironmentVariable("INVOICEXML_API_KEY") is { Length: > 0 } envApiKey)
{
    builder.Configuration["Mcp:ApiKey:Value"] = envApiKey;
}

var hostOptions = builder.Configuration
    .GetSection(McpHostOptions.SectionName)
    .Get<McpHostOptions>() ?? new McpHostOptions();

builder.Services
    .AddInvoiceXmlMcpCore(builder.Configuration)
    .AddHostAuth(builder.Configuration, hostOptions.AuthMode);

builder.Services
    .AddMcpServer(options =>
    {
        // Advertised in the MCP initialize response. ServerInfo gives clients a
        // friendly display name ("InvoiceXML") instead of the package id; the
        // instructions describe the server + tools and orient the model before
        // any call. Both are deployment-agnostic so they live in Core.
        options.ServerInfo = new Implementation
        {
            Name = "invoicexml-mcp",
            Title = "InvoiceXML",
            Version = "1.0.0",
            Icons =
            [
                new Icon
                {
                    Source = InvoiceXml.Mcp.Core.Extensions.McpServerBuilderExtensions.IconSvgDataUri,
                    MimeType = "image/svg+xml",
                    Sizes = ["any"],
                },
            ],
        };
        options.ServerInstructions = InvoiceXml.Mcp.Core.Extensions.McpServerBuilderExtensions.ServerInstructions;
    })
    .WithHttpTransport(options =>
    {
        // Stateless: every POST is a self-contained JSON-RPC call; no
        // Mcp-Session-Id handshake, no SSE stream, no per-session state on the
        // server. Lets the server run behind a load balancer / multiple
        // instances and makes the endpoint trivially testable with `curl -X POST`.
        // Our tools are synchronous request/response so we don't need SSE.
        options.Stateless = true;
    })
    .WithInvoiceXmlTools();

var app = builder.Build();

// GET / serves a friendly HTML welcome. The MCP endpoint also lives at /,
// but it only answers POSTs (JSON-RPC) — so the welcome middleware short-circuits
// browser GETs before endpoint routing ever sees them. POSTs fall through to MapMcp.
app.UseMiddleware<WelcomePageMiddleware>();

// OAuth mode: guard the MCP endpoint (POST /) and publish protected-resource metadata
// so clients can discover the authorization server. ApiKey mode skips this entirely.
if (hostOptions.AuthMode == AuthMode.OAuth)
{
    app.UseMiddleware<BearerChallengeMiddleware>();
    app.MapMcpProtectedResourceMetadata();
}

app.MapMcp("/");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so WebApplicationFactory<Program> can spin up the host in tests.
public partial class Program;
