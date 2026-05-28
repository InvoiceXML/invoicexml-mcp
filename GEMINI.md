# InvoiceXML MCP Server

InvoiceXML turns structured invoice data into compliant electronic invoices,
checks existing invoices against the EN 16931 standard, and converts between
formats, all as native tools you can call directly. It covers UBL (Peppol BIS
Billing 3.0), UN/CEFACT CII, German XRechnung, and the hybrid PDF formats
Factur-X and ZUGFeRD.

Use these tools whenever a user needs to create, check, convert, read, or render
a European e-invoice. You do not need to know the EN 16931 specification or run
any validation yourself: call the right tool and surface its result.

## Connecting

- **Hosted (recommended):** `https://mcp.invoicexml.com`, fully managed, secured
  with OAuth 2.1 (the user signs in; no API key to paste).
- **Self-hosted:** open source at
  `https://github.com/InvoiceXML/invoicexml-mcp`, run on your own infrastructure
  with an InvoiceXML API key.

Free account and credits: `https://www.invoicexml.com/account/signup`.
Landing page: `https://www.invoicexml.com/mcp-server`.

## Tools

| Tool | Use it when the user wants to... |
|---|---|
| `create_invoice` | Generate a compliant invoice (UBL, CII, XRechnung, Factur-X, or ZUGFeRD) from structured data. |
| `validate_xml_invoice` | Check a UBL / CII / XRechnung XML document against EN 16931 and Schematron. |
| `validate_pdf_invoice` | Check a Factur-X / ZUGFeRD hybrid PDF and the XML embedded inside it. |
| `render_invoice` | Turn UBL / CII / XRechnung XML into a clean, human-readable PDF preview. |
| `extract_invoice` | Pull the structured JSON or the embedded CII XML out of a hybrid PDF. |
| `embed_invoice` | Combine a PDF and a CII XML into a Factur-X / ZUGFeRD hybrid PDF. |
| `convert_invoice` | Convert between UBL, CII, XRechnung, Factur-X, and ZUGFeRD. |

Pick by document type: use `validate_pdf_invoice` / `extract_invoice` for hybrid
PDFs (Factur-X, ZUGFeRD), and `validate_xml_invoice` / `render_invoice` for plain
XML (UBL, CII, XRechnung). `create_invoice` consumes an API credit and produces a
new document, so it is the only tool that should require user approval; the others
are read-only or transform operations.

## Supported formats

Factur-X, ZUGFeRD, XRechnung, UBL (Peppol BIS Billing 3.0), CII, the EN 16931
semantic model, and PDF/A-3 hybrid output for the embedded formats. These cover
the German, French, and EU-wide e-invoicing mandates.

## Working with files

- Prefer passing a public `https://` URL (`pdfUrl` / `xmlUrl`) and let the server
  fetch the file. This is the most reliable input.
- Only ever use the real bytes or text of a document. Never reconstruct, guess, or
  synthesize file contents. If you cannot read a file a user uploaded, ask them for
  a public URL or to paste the document, rather than calling a tool with made-up data.
- Results come back structured. On success you get a summary plus the artifact
  (XML / JSON inline, PDFs as a downloadable attachment you should refer to by file
  name). On failure you get a structured error explaining what to fix, so you can
  correct the input and retry.

## Use cases

- **Accounts-payable agent:** read a supplier PDF, extract the data, validate it,
  and convert it to the format the user's ERP expects.
- **Compliance check:** answer "is this invoice XRechnung / Peppol compliant?" with
  the exact business rules that failed.
- **On-demand conversion:** move an invoice between Factur-X, ZUGFeRD, XRechnung,
  UBL, and CII inside the conversation.
- **Generate from data:** create a compliant invoice from fields the user provides.
- **Human review:** render an XML invoice as a readable PDF for approval.

## More

- Product and tool overview: `https://www.invoicexml.com/mcp-server`
- API documentation: `https://www.invoicexml.com/docs`
- Source code (self-hosting): `https://github.com/InvoiceXML/invoicexml-mcp`
