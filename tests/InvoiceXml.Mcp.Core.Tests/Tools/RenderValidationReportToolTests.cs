using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class RenderValidationReportToolTests
{
    private const string SampleXml = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"/>";

    private static RenderValidationReportTool Build(
        CapturingInvoiceXmlClient? client = null,
        IRemoteFileFetcher? fetcher = null)
        => new(
            client ?? new CapturingInvoiceXmlClient(),
            fetcher ?? new FakeRemoteFileFetcher(Encoding.UTF8.GetBytes(SampleXml)));

    [Fact]
    public async Task XmlFormat_ForwardsXmlAsUtf8ToReportEndpoint()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.RenderValidationReportAsync(ValidationReportFormat.Ubl, CancellationToken.None, xml: SampleXml);

        Assert.False(result.IsError ?? false);
        Assert.Equal(ValidationReportFormat.Ubl, client.LastReportFormat);
        Assert.Equal("application/xml", client.LastReportContentType);
        Assert.Equal(SampleXml, Encoding.UTF8.GetString(client.LastReportContent!));
    }

    [Fact]
    public async Task PdfFormat_ForwardsPdfBytes()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);
        // Minimal payload with the %%EOF trailer so the incomplete-PDF sniffer accepts it.
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n...\n%%EOF");

        var result = await tool.RenderValidationReportAsync(
            ValidationReportFormat.FacturX, CancellationToken.None, pdfBase64: Convert.ToBase64String(pdf));

        Assert.False(result.IsError ?? false);
        Assert.Equal(ValidationReportFormat.FacturX, client.LastReportFormat);
        Assert.Equal("application/pdf", client.LastReportContentType);
        Assert.Equal(pdf, client.LastReportContent);
    }

    [Fact]
    public async Task Verdict_StatedInSummary()
    {
        var client = new CapturingInvoiceXmlClient { ReportVerdict = false };
        var tool = Build(client);

        var result = await tool.RenderValidationReportAsync(ValidationReportFormat.Cii, CancellationToken.None, xml: SampleXml);

        var summary = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.Contains("NOT compliant", summary.Text);
    }

    [Fact]
    public async Task PdfArtifact_DeliveredAsEmbeddedResourceNotInlineText()
    {
        var tool = Build();

        var result = await tool.RenderValidationReportAsync(ValidationReportFormat.Ubl, CancellationToken.None, xml: SampleXml);

        Assert.False(result.IsError ?? false);
        Assert.Equal(2, result.Content.Count);
        Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
    }

    [Fact]
    public async Task NoInput_ReturnsInputRequired()
    {
        var tool = Build();

        var result = await tool.RenderValidationReportAsync(ValidationReportFormat.Ubl, CancellationToken.None);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-REQUIRED", block.Text);
    }

    [Fact]
    public async Task XmlInputForPdfFormat_ReturnsSourceMismatch()
    {
        var tool = Build();

        var result = await tool.RenderValidationReportAsync(ValidationReportFormat.Zugferd, CancellationToken.None, xml: SampleXml);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-SOURCE-MISMATCH", block.Text);
    }

    [Fact]
    public async Task PdfInputForXmlFormat_ReturnsSourceMismatch()
    {
        var tool = Build();

        var result = await tool.RenderValidationReportAsync(
            ValidationReportFormat.XRechnung, CancellationToken.None, pdfUrl: "https://example.com/invoice.pdf");

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-SOURCE-MISMATCH", block.Text);
    }
}
