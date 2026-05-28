# Contributing

Thanks for considering a contribution. A few ground rules that keep the codebase
maintainable:

## Design principles

This repository is intentionally small. Before adding code, please skim the
**Architecture** section of [README.md](README.md). The key invariants:

1. **`InvoiceXml.Mcp.Core` knows nothing about authentication.** Credentials are
   attached by the host via an `HttpClient` `DelegatingHandler`. If you find
   yourself adding an `ApiKey` property to `InvoiceXmlClientOptions`, stop.
2. **Adding a tool means adding one method on `IInvoiceXmlClient` and one
   `[McpServerTool]` class.** No changes to wiring, no edits to `Program.cs`.
3. **Adding an auth mode means adding one arm to `AuthExtensions.AddHostAuth`
   and one folder under `Host/Auth/<Mode>/`.** No edits to Core.
4. **Folder convention:** in every `InvoiceXml.*` class library, contracts live
   in `Interfaces/` and implementations in `Services/`. The folder mirrors the
   namespace.
5. **No secrets, ever, in any committed file.** That includes `appsettings.Development.json`
   (gitignored), `.env*` files, and any sample config in docs.

## Development workflow

```pwsh
# Set up your local API key once (kept outside the repo):
dotnet user-secrets --project src/InvoiceXml.Mcp.Host set "Mcp:ApiKey:Value" "<your key>"

# Build + run tests
dotnet build
dotnet test

# Run the host
dotnet run --project src/InvoiceXml.Mcp.Host
```

## Dependencies

Add or upgrade dependencies in `Directory.Packages.props` (central package
management) — never in individual `.csproj` files.

## Coding style

`.editorconfig` is authoritative. `TreatWarningsAsErrors` is on; analyzer
warnings must be fixed, not suppressed. Prefer file-scoped namespaces and
expression-bodied members where they're clearer.

## Pull requests

- One concern per PR. Refactors and feature work go in separate PRs.
- `InvoiceXml.Mcp.Core` is consumed in-repo by the host via ProjectReference;
  treat its public surface as a stable contract and flag any breaking change in
  the PR description.
- Tests are required for new functionality.
