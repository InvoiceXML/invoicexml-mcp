using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class ExtractInvoiceToolTests
{
    // A minimal *complete* PDF: %PDF header + %%EOF trailer.
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    private static ExtractInvoiceTool Build(
        CapturingInvoiceXmlClient? client = null,
        IRemoteFileFetcher? fetcher = null)
        => new(
            client ?? new CapturingInvoiceXmlClient(),
            fetcher ?? new FakeRemoteFileFetcher(SamplePdf));

    [Fact]
    public async Task Base64Mode_Json_ForwardsBytesAndReturnsInlineJson()
    {
        var jsonArtifact = new DocumentArtifact
        {
            Content = Encoding.UTF8.GetBytes("{\"invoiceNumber\":\"1\"}"),
            ContentType = "application/json",
            FileName = "invoice.json",
        };
        var client = new CapturingInvoiceXmlClient(artifact: jsonArtifact);
        var tool = Build(client);

        var result = await tool.ExtractInvoiceAsync(
            ExtractTarget.Json, CancellationToken.None, pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.False(result.IsError ?? false);
        Assert.Equal(ExtractTarget.Json, client.LastExtractTarget);
        Assert.Equal(SamplePdf, client.LastExtractContent);

        // application/json is textual → inline as a second text block.
        Assert.Equal(2, result.Content.Count);
        var body = Assert.IsType<TextContentBlock>(result.Content[1]);
        Assert.Contains("invoiceNumber", body.Text);
    }

    [Fact]
    public async Task UrlMode_Xml_FetchesAndForwards()
    {
        var client = new CapturingInvoiceXmlClient(artifact: new DocumentArtifact
        {
            Content = Encoding.UTF8.GetBytes("<CrossIndustryInvoice/>"),
            ContentType = "application/xml",
            FileName = "invoice.xml",
        });
        var fetcher = new FakeRemoteFileFetcher(SamplePdf);
        var tool = Build(client, fetcher);

        await tool.ExtractInvoiceAsync(
            ExtractTarget.Xml, CancellationToken.None, pdfUrl: "https://example.com/invoice.pdf");

        Assert.Equal("https://example.com/invoice.pdf", fetcher.LastUrl);
        Assert.Equal(SamplePdf, client.LastExtractContent);
    }

    [Fact]
    public async Task InvalidBase64_ReturnsInputBase64()
    {
        var tool = Build();

        var result = await tool.ExtractInvoiceAsync(
            ExtractTarget.Json, CancellationToken.None, pdfBase64: "not-base64-!@#$");

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-BASE64", block.Text);
    }

    [Fact]
    public async Task IncompletePdf_RejectedBeforeApiCall()
    {
        var incomplete = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <<>> endobj");
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.ExtractInvoiceAsync(
            ExtractTarget.Xml, CancellationToken.None, pdfBase64: Convert.ToBase64String(incomplete));

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-INCOMPLETE-PDF", block.Text);
        Assert.Null(client.LastExtractContent); // API not called → no credit spent
    }

    [Fact]
    public async Task AttachmentsTarget_XmlInput_ForwardsXmlContentType()
    {
        var zipArtifact = new DocumentArtifact
        {
            Content = [0x50, 0x4B, 0x03, 0x04], // "PK.."
            ContentType = "application/zip",
            FileName = "invoice-attachments.zip",
        };
        var client = new CapturingInvoiceXmlClient(artifact: zipArtifact);
        var tool = Build(client);
        const string invoiceXml = "<CrossIndustryInvoice/>";

        var result = await tool.ExtractInvoiceAsync(
            ExtractTarget.Attachments, CancellationToken.None, xml: invoiceXml);

        Assert.False(result.IsError ?? false);
        Assert.Equal(ExtractTarget.Attachments, client.LastExtractTarget);
        Assert.Equal("application/xml", client.LastExtractContentType);
        Assert.Equal(invoiceXml, Encoding.UTF8.GetString(client.LastExtractContent!));

        // application/zip is binary → delivered as an embedded resource attachment.
        Assert.Equal(2, result.Content.Count);
        Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
    }

    [Fact]
    public async Task AttachmentsTarget_PdfInput_StillForwardsPdfContentType()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        await tool.ExtractInvoiceAsync(
            ExtractTarget.Attachments, CancellationToken.None, pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.Equal("application/pdf", client.LastExtractContentType);
        Assert.Equal(SamplePdf, client.LastExtractContent);
    }

    [Fact]
    public async Task XmlInputForJsonTarget_ReturnsSourceMismatch()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.ExtractInvoiceAsync(
            ExtractTarget.Json, CancellationToken.None, xml: "<CrossIndustryInvoice/>");

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-SOURCE-MISMATCH", block.Text);
        Assert.Null(client.LastExtractContent); // API not called → no credit spent
    }

    [Fact]
    public async Task NoInput_ReturnsInputRequired()
    {
        var tool = Build();

        var result = await tool.ExtractInvoiceAsync(ExtractTarget.Json, CancellationToken.None);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-REQUIRED", block.Text);
    }

    [Fact]
    public async Task MultipleInputs_ReturnsInputExclusive()
    {
        var tool = Build();

        var result = await tool.ExtractInvoiceAsync(
            ExtractTarget.Json, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf),
            pdfUrl: "https://example.com/invoice.pdf");

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-EXCLUSIVE", block.Text);
    }
}
