using System.Net;
using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class CreateInvoiceToolTests
{
    [Fact]
    public async Task XmlFormat_PutsXmlInlineAsTextBlock()
    {
        const string xml = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"/>";
        var fakeClient = new FakeInvoiceXmlClient(new CreateInvoiceResult
        {
            Content = Encoding.UTF8.GetBytes(xml),
            ContentType = "application/xml",
            FileName = "invoice-1-ubl.xml",
        });

        var tool = new CreateInvoiceTool(fakeClient);
        var result = await tool.CreateInvoiceAsync(
            InvoiceFormat.Ubl,
            new InvoiceDocument { InvoiceNumber = "1", Currency = "EUR" },
            options: null,
            CancellationToken.None);

        Assert.False(result.IsError ?? false);
        Assert.Equal(2, result.Content.Count);

        var summary = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.Contains("ubl", summary.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invoice-1-ubl.xml", summary.Text);

        var body = Assert.IsType<TextContentBlock>(result.Content[1]);
        Assert.Equal(xml, body.Text);
    }

    [Fact]
    public async Task PdfFormat_PutsPdfInEmbeddedResourceBlockNotText()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };
        var fakeClient = new FakeInvoiceXmlClient(new CreateInvoiceResult
        {
            Content = pdf,
            ContentType = "application/pdf",
            FileName = "invoice-factur-x.pdf",
        });

        var tool = new CreateInvoiceTool(fakeClient);
        var result = await tool.CreateInvoiceAsync(
            InvoiceFormat.FacturX,
            new InvoiceDocument { InvoiceNumber = "1", Currency = "EUR" },
            new PdfRenderOptions { Language = PdfLanguage.EN },
            CancellationToken.None);

        Assert.False(result.IsError ?? false);
        Assert.Equal(2, result.Content.Count);

        var summary = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.Contains("facturx", summary.Text);
        Assert.Contains("embedded resource", summary.Text);
        // Critically: the summary must NOT carry the base64 itself —
        // that's the whole point of the EmbeddedResourceBlock split.
        Assert.DoesNotContain(Convert.ToBase64String(pdf), summary.Text);

        var embedded = Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
        var blob = Assert.IsType<BlobResourceContents>(embedded.Resource);
        Assert.Equal("application/pdf", blob.MimeType);
        Assert.Equal("attachment://invoice-factur-x.pdf", blob.Uri);
        // DecodedData round-trips back to the original bytes.
        Assert.Equal(pdf, blob.DecodedData.ToArray());
    }

    [Fact]
    public async Task ApiValidationError_ReturnsIsErrorWithStructuredJsonPayload()
    {
        const string problemJson = """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "detail": "The request failed validation with 2 error(s).",
              "valid": false,
              "errorCode": 4002,
              "errors": [
                {
                  "message": "Buyer postal address (BG-8) is required.",
                  "btCodes": ["BG-8"],
                  "fields": ["buyer.postalAddress"]
                },
                {
                  "message": "Buyer country code (BT-55) is required.",
                  "btCodes": ["BT-55"],
                  "fields": ["buyer.postalAddress.country"]
                }
              ]
            }
            """;

        var tool = new CreateInvoiceTool(new ThrowingInvoiceXmlClient(
            new InvoiceXmlApiException(HttpStatusCode.BadRequest, problemJson)));

        var result = await tool.CreateInvoiceAsync(
            InvoiceFormat.FacturX,
            new InvoiceDocument { InvoiceNumber = "1", Currency = "EUR" },
            options: null,
            CancellationToken.None);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));

        Assert.Contains("\"success\":false", block.Text);
        Assert.Contains("\"format\":\"facturx\"", block.Text);
        Assert.Contains("\"failureCategory\":\"Validation\"", block.Text);
        Assert.Contains("\"statusCode\":400", block.Text);
        Assert.Contains("Buyer postal address", block.Text);
        Assert.Contains("Fix the indicated fields", block.Text);
    }

    [Fact]
    public async Task UnauthorizedError_ReturnsIsErrorWithDoNotRetryGuidance()
    {
        const string problemJson = """{"title":"Unauthorized","status":401,"detail":"Invalid or inactive API key."}""";

        var tool = new CreateInvoiceTool(new ThrowingInvoiceXmlClient(
            new InvoiceXmlApiException(HttpStatusCode.Unauthorized, problemJson)));

        var result = await tool.CreateInvoiceAsync(
            InvoiceFormat.Ubl,
            new InvoiceDocument { InvoiceNumber = "1", Currency = "EUR" },
            options: null,
            CancellationToken.None);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("\"failureCategory\":\"Unauthorized\"", block.Text);
        Assert.Contains("Do not retry", block.Text);
    }

    [Fact]
    public async Task NetworkError_ReturnsIsErrorWithNetworkCategory()
    {
        var tool = new CreateInvoiceTool(new ThrowingInvoiceXmlClient(
            new HttpRequestException("Connection refused")));

        var result = await tool.CreateInvoiceAsync(
            InvoiceFormat.Cii,
            new InvoiceDocument { InvoiceNumber = "1", Currency = "EUR" },
            options: null,
            CancellationToken.None);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("\"failureCategory\":\"Network\"", block.Text);
    }

    private sealed class FakeInvoiceXmlClient(CreateInvoiceResult result) : IInvoiceXmlClient
    {
        public Task<CreateInvoiceResult> CreateInvoiceAsync(
            InvoiceFormat format,
            InvoiceDocument invoice,
            PdfRenderOptions? options,
            CancellationToken cancellationToken = default) => Task.FromResult(result);

        public Task<ValidationResult> ValidateXmlAsync(
            XmlInvoiceFormat format, string xml, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ValidationResult> ValidatePdfAsync(
            PdfInvoiceFormat format, byte[] pdf, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DocumentArtifact> RenderToPdfAsync(
            XmlInvoiceFormat format, string xml, PdfLanguage language, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DocumentArtifact> ExtractAsync(
            ExtractTarget target, byte[] pdf, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DocumentArtifact> EmbedAsync(
            PdfInvoiceFormat format, byte[] pdf, string ciiXml, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DocumentArtifact> ConvertAsync(
            InvoiceFormat source, InvoiceFormat target, byte[] content, string contentType, string fileName, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<DocumentArtifact> TransformAsync(
            InvoiceFormat target, byte[] pdf, PdfLanguage language, string? buyerReference, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class ThrowingInvoiceXmlClient(Exception ex) : IInvoiceXmlClient
    {
        public Task<CreateInvoiceResult> CreateInvoiceAsync(
            InvoiceFormat format, InvoiceDocument invoice, PdfRenderOptions? options,
            CancellationToken cancellationToken = default)
            => Task.FromException<CreateInvoiceResult>(ex);

        public Task<ValidationResult> ValidateXmlAsync(
            XmlInvoiceFormat format, string xml, CancellationToken cancellationToken = default)
            => Task.FromException<ValidationResult>(ex);

        public Task<ValidationResult> ValidatePdfAsync(
            PdfInvoiceFormat format, byte[] pdf, CancellationToken cancellationToken = default)
            => Task.FromException<ValidationResult>(ex);

        public Task<DocumentArtifact> RenderToPdfAsync(
            XmlInvoiceFormat format, string xml, PdfLanguage language, CancellationToken cancellationToken = default)
            => Task.FromException<DocumentArtifact>(ex);

        public Task<DocumentArtifact> ExtractAsync(
            ExtractTarget target, byte[] pdf, CancellationToken cancellationToken = default)
            => Task.FromException<DocumentArtifact>(ex);

        public Task<DocumentArtifact> EmbedAsync(
            PdfInvoiceFormat format, byte[] pdf, string ciiXml, CancellationToken cancellationToken = default)
            => Task.FromException<DocumentArtifact>(ex);

        public Task<DocumentArtifact> ConvertAsync(
            InvoiceFormat source, InvoiceFormat target, byte[] content, string contentType, string fileName, CancellationToken cancellationToken = default)
            => Task.FromException<DocumentArtifact>(ex);

        public Task<DocumentArtifact> TransformAsync(
            InvoiceFormat target, byte[] pdf, PdfLanguage language, string? buyerReference, CancellationToken cancellationToken = default)
            => Task.FromException<DocumentArtifact>(ex);
    }
}
