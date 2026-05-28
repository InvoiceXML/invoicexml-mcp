using Microsoft.AspNetCore.Http;

namespace InvoiceXml.Mcp.Host;

/// <summary>
/// Renders the friendly HTML landing page served for <c>GET /</c>. The MCP
/// endpoint lives at <c>POST /</c>; this middleware runs before endpoint
/// routing and short-circuits browser GETs to the root so users see a real
/// page instead of the SDK's session-required JSON error.
/// </summary>
internal sealed class WelcomePageMiddleware
{
    private const string ContentType = "text/html; charset=utf-8";

    private readonly RequestDelegate _next;

    public WelcomePageMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsWelcomeRequest(context.Request))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var mcpUrl = $"{context.Request.Scheme}://{context.Request.Host}/";

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = ContentType;
        await context.Response.WriteAsync(Render(mcpUrl)).ConfigureAwait(false);
    }

    private static bool IsWelcomeRequest(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) &&
        request.Path == "/";

    private static string Render(string mcpUrl) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="robots" content="noindex">
          <title>InvoiceXML MCP Server</title>
          <style>
            :root {
              color-scheme: light dark;
              --bg: #fafaf9;
              --fg: #1a202c;
              --muted: #64748b;
              --card: #ffffff;
              --border: #e2e8f0;
              --accent: #0f766e;
              --code-bg: #f1f5f9;
            }
            @media (prefers-color-scheme: dark) {
              :root {
                --bg: #0f172a;
                --fg: #e2e8f0;
                --muted: #94a3b8;
                --card: #1e293b;
                --border: #334155;
                --accent: #5eead4;
                --code-bg: #0b1220;
              }
            }
            * { box-sizing: border-box; }
            body {
              margin: 0;
              padding: 3rem 1.25rem;
              background: var(--bg);
              color: var(--fg);
              font: 16px/1.55 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            }
            main { max-width: 720px; margin: 0 auto; }
            h1 { margin: 0 0 0.5rem; font-size: 1.75rem; letter-spacing: -0.01em; }
            .lede { margin: 0 0 2rem; color: var(--muted); font-size: 1.0625rem; }
            h2 { margin: 2rem 0 0.75rem; font-size: 1.0625rem; letter-spacing: -0.005em; }
            ul { padding-left: 1.25rem; margin: 0 0 1rem; }
            li { margin-bottom: 0.5rem; }
            code, pre { font-family: "SF Mono", Monaco, Consolas, monospace; font-size: 0.875rem; }
            code { background: var(--code-bg); padding: 0.125rem 0.375rem; border-radius: 4px; }
            pre {
              background: var(--code-bg);
              padding: 0.875rem 1rem;
              border-radius: 6px;
              border: 1px solid var(--border);
              overflow-x: auto;
              margin: 0.5rem 0 0;
            }
            pre code { background: none; padding: 0; }
            a { color: var(--accent); text-decoration: none; }
            a:hover { text-decoration: underline; }
            .pill {
              display: inline-block;
              background: var(--accent);
              color: var(--bg);
              padding: 0.125rem 0.5rem;
              border-radius: 999px;
              font-size: 0.6875rem;
              font-weight: 600;
              letter-spacing: 0.04em;
              text-transform: uppercase;
              vertical-align: 0.2em;
            }
            .card {
              background: var(--card);
              border: 1px solid var(--border);
              border-radius: 8px;
              padding: 1rem 1.25rem;
              margin-top: 0.75rem;
            }
            .foot {
              margin-top: 3rem;
              padding-top: 1rem;
              border-top: 1px solid var(--border);
              font-size: 0.8125rem;
              color: var(--muted);
            }
          </style>
        </head>
        <body>
          <main>
            <h1>InvoiceXML MCP Server <span class="pill">running</span></h1>
            <p class="lede">
              This is a Model Context Protocol server, not a website. Connect it to an AI agent
              to call the InvoiceXML API through natural language.
            </p>

            <h2>Connect a client</h2>
            <div class="card">
              <strong>MCP Inspector</strong> (quickest way to try it):
              <pre><code>npx @modelcontextprotocol/inspector</code></pre>
              In the Inspector UI, choose transport <em>Streamable HTTP</em> and use:
              <pre><code>{{mcpUrl}}</code></pre>
            </div>

            <div class="card">
              <strong>Claude Desktop</strong>: edit your <code>claude_desktop_config.json</code>:
              <pre><code>{
          "mcpServers": {
            "invoicexml": { "url": "{{mcpUrl}}" }
          }
        }</code></pre>
            </div>

            <div class="card">
              <strong>Anything else</strong>: POST a JSON-RPC 2.0 envelope to <code>{{mcpUrl}}</code>.
              See the <a href="https://modelcontextprotocol.io">MCP specification</a> for the wire format.
            </div>

            <h2>What this server exposes</h2>
            <ul>
              <li><code>create_invoice</code>: build a UBL, CII, XRechnung, Factur-X or ZUGFeRD invoice from structured fields.</li>
              <li><code>validate_xml_invoice</code>: validate UBL, CII or XRechnung XML against EN 16931 + Schematron.</li>
              <li><code>validate_pdf_invoice</code>: validate the embedded XML of a Factur-X or ZUGFeRD hybrid PDF.</li>
            </ul>

            <p class="foot">
              Powered by <a href="https://invoicexml.com">InvoiceXML</a>.
              Health endpoint: <a href="/health">/health</a>.
              Source: <a href="https://github.com/invoicexml/invoicexml-mcp">github.com/invoicexml/invoicexml-mcp</a>.
            </p>
          </main>
        </body>
        </html>
        """;
}
