using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class RenderInvoiceToolTests
{
    private const string SampleXml = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"/>";

    private static RenderInvoiceTool Build(
        CapturingInvoiceXmlClient? client = null,
        IRemoteFileFetcher? fetcher = null)
        => new(
            client ?? new CapturingInvoiceXmlClient(),
            fetcher ?? new FakeRemoteFileFetcher(Encoding.UTF8.GetBytes(SampleXml)));

    [Fact]
    public async Task InlineMode_ForwardsXmlAndDefaultsLanguageToEn()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.RenderInvoiceAsync(XmlInvoiceFormat.Ubl, CancellationToken.None, xml: SampleXml);

        Assert.False(result.IsError ?? false);
        Assert.Equal(XmlInvoiceFormat.Ubl, client.LastRenderFormat);
        Assert.Equal(SampleXml, client.LastRenderXml);
        Assert.Equal(PdfLanguage.EN, client.LastRenderLanguage);
    }

    [Fact]
    public async Task UrlMode_FetchesDecodesAndForwards()
    {
        var client = new CapturingInvoiceXmlClient();
        var fetcher = new FakeRemoteFileFetcher(Encoding.UTF8.GetBytes(SampleXml));
        var tool = Build(client, fetcher);

        await tool.RenderInvoiceAsync(XmlInvoiceFormat.Cii, CancellationToken.None,
            xmlUrl: "https://example.com/invoice.xml");

        Assert.Equal("https://example.com/invoice.xml", fetcher.LastUrl);
        Assert.Equal(SampleXml, client.LastRenderXml);
    }

    [Fact]
    public async Task Language_ForwardedWhenSpecified()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        await tool.RenderInvoiceAsync(XmlInvoiceFormat.XRechnung, CancellationToken.None,
            xml: SampleXml, language: PdfLanguage.DE);

        Assert.Equal(PdfLanguage.DE, client.LastRenderLanguage);
    }

    [Fact]
    public async Task PdfArtifact_DeliveredAsEmbeddedResourceNotInlineText()
    {
        var tool = Build();

        var result = await tool.RenderInvoiceAsync(XmlInvoiceFormat.Ubl, CancellationToken.None, xml: SampleXml);

        Assert.False(result.IsError ?? false);
        Assert.Equal(2, result.Content.Count);
        Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
    }

    [Fact]
    public async Task NoInput_ReturnsInputRequired()
    {
        var tool = Build();

        var result = await tool.RenderInvoiceAsync(XmlInvoiceFormat.Ubl, CancellationToken.None);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-REQUIRED", block.Text);
    }

    [Fact]
    public async Task BothInputs_ReturnsInputExclusive()
    {
        var tool = Build();

        var result = await tool.RenderInvoiceAsync(XmlInvoiceFormat.Ubl, CancellationToken.None,
            xml: SampleXml, xmlUrl: "https://example.com/invoice.xml");

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-EXCLUSIVE", block.Text);
    }
}
