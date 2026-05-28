using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class EmbedInvoiceToolTests
{
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");
    private const string SampleCii = "<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\"/>";

    private static EmbedInvoiceTool Build(
        CapturingInvoiceXmlClient? client = null,
        IRemoteFileFetcher? fetcher = null)
        => new(
            client ?? new CapturingInvoiceXmlClient(),
            fetcher ?? new FakeRemoteFileFetcher(SamplePdf));

    [Fact]
    public async Task PdfBase64AndInlineXml_ForwardsBothToClient()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.EmbedInvoiceAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf), xml: SampleCii);

        Assert.False(result.IsError ?? false);
        Assert.Equal(PdfInvoiceFormat.FacturX, client.LastEmbedFormat);
        Assert.Equal(SamplePdf, client.LastEmbedPdf);
        Assert.Equal(SampleCii, client.LastEmbedXml);
        Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
    }

    [Fact]
    public async Task PdfUrlAndInlineXml_FetchesPdfAndForwards()
    {
        var client = new CapturingInvoiceXmlClient();
        var fetcher = new FakeRemoteFileFetcher(SamplePdf);
        var tool = Build(client, fetcher);

        await tool.EmbedInvoiceAsync(
            PdfInvoiceFormat.Zugferd, CancellationToken.None,
            pdfUrl: "https://example.com/invoice.pdf", xml: SampleCii);

        Assert.Equal("https://example.com/invoice.pdf", fetcher.LastUrl);
        Assert.Equal(SamplePdf, client.LastEmbedPdf);
        Assert.Equal(SampleCii, client.LastEmbedXml);
    }

    [Fact]
    public async Task MissingPdf_ReturnsInputRequired()
    {
        var tool = Build();

        var result = await tool.EmbedInvoiceAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None, xml: SampleCii);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-REQUIRED", block.Text);
    }

    [Fact]
    public async Task MissingXml_ReturnsInputRequired()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.EmbedInvoiceAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None, pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-REQUIRED", block.Text);
        Assert.Null(client.LastEmbedPdf); // rejected before any API call
    }

    [Fact]
    public async Task IncompletePdf_RejectedBeforeApiCall()
    {
        var incomplete = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <<>> endobj");
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.EmbedInvoiceAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(incomplete), xml: SampleCii);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-INCOMPLETE-PDF", block.Text);
        Assert.Null(client.LastEmbedPdf);
    }

    [Fact]
    public async Task InvalidBase64_ReturnsInputBase64()
    {
        var tool = Build();

        var result = await tool.EmbedInvoiceAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: "not-base64-!@#$", xml: SampleCii);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-BASE64", block.Text);
    }
}
