# InvoiceXML MCP Server

A [Model Context Protocol](https://modelcontextprotocol.io) server that exposes the
[InvoiceXML API](https://invoicexml.com) to AI agents. Covers Factur-X, ZUGFeRD,
XRechnung, UBL / CII, and Peppol BIS Billing 3.0.

The same codebase runs in two deployment shapes, selected at startup by one
environment variable:

| Mode | Who runs it | Auth |
|---|---|---|
| Self-hosted | You, on your own machine or server | A single API key in env or `appsettings.json` |
| Hosted | InvoiceXML, on its own infrastructure | OAuth 2.1 + Dynamic Client Registration against `invoicexml.com` |

Both run the same binary; only the configuration differs. The repository is
platform-independent — it knows nothing about where or how you host it.

## Architecture

```
+----------------------+   ProjectReference   +----------------------+
|  InvoiceXml.Mcp.Core | -------------------> |  InvoiceXml.Mcp.Host |
|  (SDK: client+tools) |                      |  (the deployable)    |
+----------------------+                      +----------------------+
```

**`InvoiceXml.Mcp.Core`** is a small, transport-agnostic SDK:

- `IInvoiceXmlClient` — typed client over the public REST API
- `HttpInvoiceXmlClient` — the only implementation; consumes an `HttpClient` from `IHttpClientFactory`
- `InvoiceXmlClientOptions` — base URL, timeout (no auth)
- `AddInvoiceXmlMcpCore(IServiceCollection, IConfiguration)` — DI entry point; returns the `IHttpClientBuilder` so the host attaches auth as a `DelegatingHandler`

The SDK **never sees credentials**. The host applies them through the HTTP pipeline.
That seam is what lets one codebase serve both deployment modes.

**`InvoiceXml.Mcp.Host`** is an ASP.NET Core 10 app:

- Reads `Mcp:AuthMode` (`ApiKey` or `OAuth`) at startup
- Wires the matching `DelegatingHandler` onto the Core HTTP client via `AddHostAuth(...)`
- Serves the MCP endpoint at `POST /`, a human-friendly welcome page at `GET /`, and `/health`

Adding a new auth mode = one arm in `AuthExtensions.cs` plus a small folder under
`Auth/<Mode>/`. Adding a new tool = one `[McpServerTool]` class. Nothing else changes.

## Repository layout

```
invoicexml-mcp/
├── src/
│   ├── InvoiceXml.Mcp.Core/              # the SDK: client, models, tools
│   │   ├── Enums/  Interfaces/  Models/  Options/  Services/  Tools/  Extensions/
│   └── InvoiceXml.Mcp.Host/              # the deployable host
│       ├── Auth/{ApiKey,OAuth}/          # the two auth modes
│       ├── Configuration/
│       ├── Program.cs
│       └── appsettings.json              # safe defaults, no secrets
├── tests/
│   ├── InvoiceXml.Mcp.Core.Tests/
│   └── InvoiceXml.Mcp.Host.Tests/
├── Directory.Build.props                 # repo-wide MSBuild defaults
├── Directory.Packages.props              # Central Package Management
├── global.json                           # pins the .NET SDK
└── InvoiceXml.Mcp.slnx
```

## Running locally

You need a .NET 10 SDK and an InvoiceXML API key.

```pwsh
# 1. Provide your API key (pick one):

# A. dotnet user-secrets (recommended — kept outside the repo)
dotnet user-secrets --project src/InvoiceXml.Mcp.Host set "Mcp:ApiKey:Value" "your-key"

# B. environment variable
$env:INVOICEXML_API_KEY = "your-key"

# 2. Run
dotnet run --project src/InvoiceXml.Mcp.Host
```

`GET http://localhost:5004/` shows a welcome page in a browser; the MCP endpoint is
`POST http://localhost:5004/`; `GET /health` returns `{ "status": "ok" }`.

## Configuration

```jsonc
{
  // Required in OAuth mode. The public origin where this MCP server is reachable.
  // Used in the protected-resource metadata response.
  "McpUri": "https://mcp.example.com",

  "InvoiceXml": {
    "BaseUrl": "https://api.invoicexml.com",   // override for staging / local
    "Timeout": "00:01:40"
  },

  "Mcp": {
    "AuthMode": "ApiKey",                       // "ApiKey" | "OAuth"
    "ApiKey": {
      "Value": ""                               // ApiKey mode: NEVER commit a real key
    },
    "OAuth": {
      "AuthorizationServer": "https://invoicexml.com",
      "ScopesSupported": [ "api_token.read" ]
    },
    "FileInput": {                              // limits for the URL-fetch input mode
      "MaxFileSizeBytes": 5242880,
      "FetchTimeout": "00:00:30"
    }
  }
}
```

Environment variable equivalents (double underscore = nesting):

| Variable | Maps to |
|---|---|
| `INVOICEXML_API_KEY` | `Mcp:ApiKey:Value` (friendly alias) |
| `Mcp__ApiKey__Value` | `Mcp:ApiKey:Value` |
| `Mcp__AuthMode` | `Mcp:AuthMode` (`ApiKey` or `OAuth`) |
| `Mcp__OAuth__AuthorizationServer` | `Mcp:OAuth:AuthorizationServer` |
| `McpUri` | `McpUri` (root-level) |
| `InvoiceXml__BaseUrl` | `InvoiceXml:BaseUrl` |

## OAuth mode

When `Mcp:AuthMode=OAuth` the host stops accepting a static API key and instead:

1. Returns **401** with `WWW-Authenticate: Bearer resource_metadata="…"` for any
   `POST /` that has no Bearer token.
2. Serves `GET /.well-known/oauth-protected-resource` pointing MCP clients at
   `invoicexml.com` as the authorization server.
3. Forwards the inbound Bearer token verbatim on every outbound call to the
   InvoiceXML API (the API is the source of truth for token validity; the MCP
   server does not validate tokens locally).

The dance an MCP client performs:

```
client → MCP  POST /                           → 401 + resource_metadata
client → /.well-known/oauth-protected-resource → { authorization_servers: [invoicexml.com] }
client → invoicexml.com/.well-known/oauth-authorization-server
                                               → { authorize, token, register endpoints }
client → invoicexml.com/oauth/register         → client_id + client_secret  (DCR)
client → invoicexml.com/oauth/authorize        → user consents, gets code
client → invoicexml.com/oauth/token            → access_token  (= user's API key)
client → MCP  POST /  + Authorization: Bearer  → 200, tool call flows through
```

## Deployment

The host is a standard ASP.NET Core app — run it however you run .NET services
(systemd, a container, a PaaS, etc.; the repo doesn't prescribe one):

```pwsh
dotnet publish src/InvoiceXml.Mcp.Host -c Release -o ./publish
# then run ./publish/InvoiceXml.Mcp.Host on your host
```

Set configuration via environment variables on the host (never commit secrets):

- `ASPNETCORE_ENVIRONMENT=Production`
- `Mcp__AuthMode=ApiKey` (or `OAuth`)
- `Mcp__ApiKey__Value=…` / `INVOICEXML_API_KEY=…` (ApiKey mode)
- `McpUri=https://your-public-url` and `Mcp__OAuth__AuthorizationServer=https://invoicexml.com` (OAuth mode)

Terminate TLS at your reverse proxy / load balancer and forward to the host's HTTP port.
The server is stateless, so you can run multiple instances behind a load balancer.

## License

MIT — see [LICENSE](LICENSE).
