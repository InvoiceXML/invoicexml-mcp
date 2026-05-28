# InvoiceXML MCP Server: installation guide for AI agents

These instructions are written for an AI assistant (such as Cline) to install and
configure the InvoiceXML MCP server. Follow the recommended path unless the user
explicitly asks to self-host.

The server exposes tools to create, validate, convert, render, extract, and embed
compliant e-invoices (UBL / Peppol BIS Billing 3.0, CII, Factur-X, ZUGFeRD,
XRechnung) checked against EN 16931.

## Recommended: connect to the hosted server (no build, no API key)

The hosted server is fully managed and authenticates with OAuth 2.1, so the user
does not paste any API key.

1. Open the Cline MCP settings file (`cline_mcp_settings.json`).
2. Add this entry under `mcpServers` (it is a remote, streamable-HTTP server):

   ```json
   {
     "mcpServers": {
       "invoicexml": {
         "type": "streamableHttp",
         "url": "https://mcp.invoicexml.com"
       }
     }
   }
   ```

   If this Cline version uses different keys for a remote MCP server, configure it
   as a remote server using the **Streamable HTTP** transport with the URL
   `https://mcp.invoicexml.com`. Do not configure a command/stdio server.
3. Save and let Cline (re)connect to the server.
4. On the first tool call, the server returns a 401 and Cline starts the OAuth
   flow: the user signs in at `invoicexml.com` in the browser and approves access.
   No secret needs to be stored in the config.
5. Verify the connection by listing the server's tools. You should see:
   `create_invoice`, `validate_xml_invoice`, `validate_pdf_invoice`,
   `render_invoice`, `extract_invoice`, `embed_invoice`, `convert_invoice`.

The user can create a free account with starter credits at
`https://www.invoicexml.com/account/signup`.

## Alternative: self-hosted (advanced)

Only use this if the user wants to run the open-source server themselves. It
requires a .NET 10 SDK and an InvoiceXML API key.

1. Clone `https://github.com/InvoiceXML/invoicexml-mcp`.
2. Provide the API key, kept out of source:
   `dotnet user-secrets --project src/InvoiceXml.Mcp.Host set "Mcp:ApiKey:Value" "<key>"`
   (or set the environment variable `INVOICEXML_API_KEY`).
3. Run: `dotnet run --project src/InvoiceXml.Mcp.Host`.
4. Add the local server to `cline_mcp_settings.json` as a remote Streamable HTTP
   server pointing at the host's printed URL (for example `http://localhost:5004`).

See the repository `README.md` for full configuration (ports, OAuth mode,
environment variables).

## Notes for the assistant

- Prefer the hosted option; it needs no toolchain and no key.
- When a tool accepts a file, pass a public `https://` URL (for example `pdfUrl`
  or `xmlUrl`) rather than fabricating file contents.
- Do not store any API key in the Cline config when using the hosted server.
