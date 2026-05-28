using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class ValidateXmlInvoiceToolTests
{
    private const string SampleXml = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"/>";

    private static ValidateXmlInvoiceTool Build(
        CapturingInvoiceXmlClient? client = null,
        IRemoteFileFetcher? fetcher = null)
        => new(
            client ?? new CapturingInvoiceXmlClient(),
            fetcher ?? new FakeRemoteFileFetcher(Encoding.UTF8.GetBytes(SampleXml)));

    [Fact]
    public async Task InlineMode_ForwardsXmlTextToClient()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        await tool.ValidateXmlAsync(XmlInvoiceFormat.Ubl, CancellationToken.None, xml: SampleXml);

        Assert.Equal(XmlInvoiceFormat.Ubl, client.LastXmlFormat);
        Assert.Equal(SampleXml, client.LastXml);
    }

    [Fact]
    public async Task UrlMode_FetchesDecodesAndForwards()
    {
        var client = new CapturingInvoiceXmlClient();
        var fetcher = new FakeRemoteFileFetcher(Encoding.UTF8.GetBytes(SampleXml));
        var tool = Build(client, fetcher);

        await tool.ValidateXmlAsync(XmlInvoiceFormat.XRechnung, CancellationToken.None,
            xmlUrl: "https://example.com/invoice.xml");

        Assert.Equal("https://example.com/invoice.xml", fetcher.LastUrl);
        Assert.Equal(SampleXml, client.LastXml);
    }

    [Fact]
    public async Task UrlMode_StripsUtf8Bom()
    {
        var client = new CapturingInvoiceXmlClient();
        // UTF-8 BOM (EF BB BF) prepended to the XML bytes.
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(SampleXml)).ToArray();
        var tool = Build(client, new FakeRemoteFileFetcher(bytes));

        await tool.ValidateXmlAsync(XmlInvoiceFormat.Cii, CancellationToken.None,
            xmlUrl: "https://example.com/bom.xml");

        Assert.Equal(SampleXml, client.LastXml); // BOM removed
    }

    [Fact]
    public async Task NoInput_ReturnsInputRequired()
    {
        var tool = Build();

        var result = await tool.ValidateXmlAsync(XmlInvoiceFormat.Ubl, CancellationToken.None);

        Assert.False(result.Valid);
        Assert.Contains(result.Errors!, e => e.Rule == "INPUT-REQUIRED");
    }

    [Fact]
    public async Task MultipleInputs_ReturnsInputExclusive()
    {
        var tool = Build();

        var result = await tool.ValidateXmlAsync(XmlInvoiceFormat.Ubl, CancellationToken.None,
            xml: SampleXml, xmlUrl: "https://example.com/invoice.xml");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors!, e => e.Rule == "INPUT-EXCLUSIVE");
    }
}
