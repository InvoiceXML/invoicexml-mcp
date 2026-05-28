using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class ConvertInvoiceToolTests
{
    private const string SampleXml = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"/>";
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    private static ConvertInvoiceTool Build(
        CapturingInvoiceXmlClient? client = null,
        IRemoteFileFetcher? fetcher = null)
        => new(
            client ?? new CapturingInvoiceXmlClient(),
            fetcher ?? new FakeRemoteFileFetcher(SamplePdf));

    [Fact]
    public async Task XmlSource_ForwardsXmlBytesAndForkRoute()
    {
        var xmlArtifact = new DocumentArtifact
        {
            Content = Encoding.UTF8.GetBytes("<CrossIndustryInvoice/>"),
            ContentType = "application/xml",
            FileName = "invoice-cii.xml",
        };
        var client = new CapturingInvoiceXmlClient(artifact: xmlArtifact);
        var tool = Build(client);

        var result = await tool.ConvertInvoiceAsync(
            InvoiceFormat.Ubl, InvoiceFormat.Cii, CancellationToken.None, xml: SampleXml);

        Assert.False(result.IsError ?? false);
        Assert.Equal(InvoiceFormat.Ubl, client.LastConvertSource);
        Assert.Equal(InvoiceFormat.Cii, client.LastConvertTarget);
        Assert.Equal("application/xml", client.LastConvertContentType);
        Assert.Equal(Encoding.UTF8.GetBytes(SampleXml), client.LastConvertContent);
        // XML target is textual → inline second text block.
        Assert.IsType<TextContentBlock>(result.Content[1]);
    }

    [Fact]
    public async Task PdfSource_ForwardsPdfBytes()
    {
        var client = new CapturingInvoiceXmlClient(artifact: new DocumentArtifact
        {
            Content = Encoding.UTF8.GetBytes("<CrossIndustryInvoice/>"),
            ContentType = "application/xml",
            FileName = "invoice-cii.xml",
        });
        var tool = Build(client);

        var result = await tool.ConvertInvoiceAsync(
            InvoiceFormat.FacturX, InvoiceFormat.Cii, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.False(result.IsError ?? false);
        Assert.Equal(InvoiceFormat.FacturX, client.LastConvertSource);
        Assert.Equal(InvoiceFormat.Cii, client.LastConvertTarget);
        Assert.Equal("application/pdf", client.LastConvertContentType);
        Assert.Equal(SamplePdf, client.LastConvertContent);
    }

    [Fact]
    public async Task UnsupportedPair_RejectedBeforeApiCall()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        // ubl -> ubl is a no-op the API does not expose.
        var result = await tool.ConvertInvoiceAsync(
            InvoiceFormat.Ubl, InvoiceFormat.Ubl, CancellationToken.None, xml: SampleXml);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-UNSUPPORTED-CONVERSION", block.Text);
        Assert.Null(client.LastConvertSource);
    }

    [Fact]
    public async Task XmlSourceWithPdfInput_ReturnsSourceMismatch()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.ConvertInvoiceAsync(
            InvoiceFormat.Ubl, InvoiceFormat.Cii, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-SOURCE-MISMATCH", block.Text);
        Assert.Null(client.LastConvertSource);
    }

    [Fact]
    public async Task PdfSourceWithXmlInput_ReturnsSourceMismatch()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.ConvertInvoiceAsync(
            InvoiceFormat.FacturX, InvoiceFormat.Cii, CancellationToken.None, xml: SampleXml);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-SOURCE-MISMATCH", block.Text);
        Assert.Null(client.LastConvertSource);
    }

    [Fact]
    public async Task NoInput_ReturnsInputRequired()
    {
        var tool = Build();

        var result = await tool.ConvertInvoiceAsync(
            InvoiceFormat.Ubl, InvoiceFormat.Cii, CancellationToken.None);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-REQUIRED", block.Text);
    }

    [Fact]
    public async Task IncompletePdfSource_RejectedBeforeApiCall()
    {
        var incomplete = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <<>> endobj");
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.ConvertInvoiceAsync(
            InvoiceFormat.FacturX, InvoiceFormat.Ubl, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(incomplete));

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-INCOMPLETE-PDF", block.Text);
        Assert.Null(client.LastConvertSource);
    }
}
