# AGENTS.md

Context for AI coding agents (and humans) working **in this repository**.
Canonical prose lives in [README.md](README.md) and [CONTRIBUTING.md](CONTRIBUTING.md);
this file is the high-signal summary.

> Note: `GEMINI.md` is a different thing. It is the *end-user* context for the
> hosted MCP server, referenced by `gemini-extension.json`. Do not put repo build
> or contributor instructions there.

## What this is

A Model Context Protocol server (C# / .NET 10) that exposes the
[InvoiceXML API](https://invoicexml.com) to AI agents: create, validate, render,
extract, embed, and convert e-invoices across Factur-X, ZUGFeRD, XRechnung,
UBL / CII, and Peppol BIS Billing 3.0.

One codebase, two deployment shapes, selected at startup by `Mcp:AuthMode`:
`ApiKey` (self-hosted, single API key) or `OAuth` (hosted, OAuth 2.1 + DCR
against `invoicexml.com`). The repo is platform-independent: it knows nothing
about where or how it is hosted.

## Commands

```pwsh
dotnet build                                   # whole solution (warnings are errors)
dotnet test                                    # all tests (xUnit)
dotnet test tests/InvoiceXml.Mcp.Core.Tests/InvoiceXml.Mcp.Core.Tests.csproj
dotnet run --project src/InvoiceXml.Mcp.Host   # run the host locally
```

Requires a .NET 10 SDK (`global.json` floor is `10.0.100`, rolls forward to the
latest 10.0.x). To run, supply an API key first, kept outside the repo:
`dotnet user-secrets --project src/InvoiceXml.Mcp.Host set "Mcp:ApiKey:Value" "<key>"`.

Local endpoints: `GET /` welcome page, `POST /` MCP endpoint, `GET /health`.

## Architecture

```
InvoiceXml.Mcp.Core  --ProjectReference-->  InvoiceXml.Mcp.Host
(SDK: client + tools)                       (the deployable ASP.NET Core app)
```

- **Core** is a transport- and auth-agnostic SDK: `IInvoiceXmlClient` +
  `HttpInvoiceXmlClient`, the DTOs/enums, and the `[McpServerTool]` classes. It
  consumes an `HttpClient` from `IHttpClientFactory` and **never sees credentials**.
- **Host** reads `Mcp:AuthMode` and attaches the matching auth as an `HttpClient`
  `DelegatingHandler` via `AddHostAuth(...)`. That handler seam is the whole reason
  one codebase serves both modes.

## Invariants to preserve (do not break these)

1. **Core knows nothing about authentication.** Never add an API key or token to
   `InvoiceXmlClientOptions` or the Core client. Auth is host-side only.
2. **A new tool = one method on `IInvoiceXmlClient` + one `[McpServerTool]` class.**
   No edits to `Program.cs` or DI wiring (tools are auto-discovered from the assembly).
3. **A new auth mode = one arm in `AuthExtensions.AddHostAuth` + one folder under
   `src/InvoiceXml.Mcp.Host/Auth/<Mode>/`.** No edits to Core.
4. **Folder = namespace.** In every `InvoiceXml.*` library, `I*` contracts go in
   `Interfaces/`, concretes in `Services/`.
5. **No secrets in any committed file**, ever. `appsettings.Development.json` is
   gitignored; never put a real key in `appsettings.json`, docs, or tests.
6. **Tests are required** for new functionality.

## Tools (current MCP surface)

`create_invoice`, `validate_xml_invoice`, `validate_pdf_invoice`,
`render_invoice`, `extract_invoice`, `embed_invoice`, `convert_invoice`.

They live in `src/InvoiceXml.Mcp.Core/Tools/`. File inputs prefer a public
`https://` URL (`*Url` params) over inline base64. Tools never throw on bad input:
they return a structured failure (`ToolResults.ForFailure` or a `valid=false`
`ValidationResult`) so the agent can self-correct.

## Configuration

Keys (appsettings.json or environment variables). For env vars, each `:` becomes
`__` (double underscore); root keys have no separator.

| Config key | Env var | Notes |
|---|---|---|
| `Mcp:AuthMode` | `Mcp__AuthMode` | `ApiKey` or `OAuth` |
| `Mcp:ApiKey:Value` | `Mcp__ApiKey__Value` or `INVOICEXML_API_KEY` | ApiKey mode only |
| `Mcp:OAuth:AuthorizationServer` | `Mcp__OAuth__AuthorizationServer` | OAuth mode |
| `McpUri` | `McpUri` | root-level; required in OAuth mode |
| `InvoiceXml:BaseUrl` | `InvoiceXml__BaseUrl` | API base URL |

## Conventions

- **Central Package Management:** add or bump NuGet versions in
  `Directory.Packages.props`, never in individual `.csproj` files.
- **`.editorconfig` is authoritative.** `TreatWarningsAsErrors` is on; fix
  analyzer warnings, do not suppress them. File-scoped namespaces; expression-bodied
  members where clearer.
- **Shared build settings** live in `Directory.Build.props` (target `net10.0`,
  nullable enabled, implicit usings).
- Keep prose plain: no em dashes (use commas, parentheses, or colons).

## Where to look

- New tool? `src/InvoiceXml.Mcp.Core/Tools/` and `Interfaces/IInvoiceXmlClient.cs`.
- Auth / deployment? `src/InvoiceXml.Mcp.Host/Auth/` and `Configuration/`.
- Result shaping for tools? `src/InvoiceXml.Mcp.Core/Tools/ToolResults.cs`,
  `ArtifactTools.cs`, `ToolFailure.cs`.
- Tests mirror the source tree under `tests/`.
