using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

/// <summary>
/// The optional <c>rules</c>, <c>logoUrl</c> and <c>footerBrand</c> tool
/// parameters: forwarded where the API binds them, refused up front where an
/// API route would silently skip a requested rule set.
/// </summary>
public class RulesAndBrandingToolTests
{
    private const string SampleUbl = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"/>";
    private const string SampleCii = "<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\"/>";
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    [Theory]
    [InlineData(InvoiceFormat.Zugferd)]
    [InlineData(InvoiceFormat.XRechnung)]
    public async Task CreateInvoice_RefusesRulesOnUnsupportedFormatsWithoutCallingTheApi(InvoiceFormat format)
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new CreateInvoiceTool(client);

        var result = await tool.CreateInvoiceAsync(
            format, new InvoiceDocument { InvoiceNumber = "1", Currency = "EUR" },
            new CreateInvoiceOptions { Rules = [ExtraRuleset.BrFr] }, CancellationToken.None);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INPUT-RULES", block.Text);
        Assert.Contains("options.rules", block.Text);
        Assert.Null(client.LastCreateFormat);
    }

    [Theory]
    [InlineData(InvoiceFormat.Ubl)]
    [InlineData(InvoiceFormat.Cii)]
    [InlineData(InvoiceFormat.FacturX)]
    public async Task CreateInvoice_ForwardsRulesOnSupportedFormats(InvoiceFormat format)
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new CreateInvoiceTool(client);

        var result = await tool.CreateInvoiceAsync(
            format, new InvoiceDocument { InvoiceNumber = "1", Currency = "EUR" },
            new CreateInvoiceOptions { Rules = [ExtraRuleset.BrFr] }, CancellationToken.None);

        Assert.False(result.IsError ?? false);
        Assert.Equal(format, client.LastCreateFormat);
        Assert.Equal([ExtraRuleset.BrFr], client.LastCreateOptions!.Rules);
    }

    [Theory]
    [InlineData(XmlInvoiceFormat.Ubl)]
    [InlineData(XmlInvoiceFormat.Cii)]
    public async Task ValidateXml_ForwardsRulesOnSupportedFormats(XmlInvoiceFormat format)
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new ValidateXmlInvoiceTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.ValidateXmlAsync(format, CancellationToken.None, xml: SampleUbl, rules: [ExtraRuleset.BrFr]);

        Assert.True(result.Valid);
        Assert.Equal([ExtraRuleset.BrFr], client.LastRules);
    }

    [Fact]
    public async Task ValidateXml_RefusesRulesOnXRechnungWithoutCallingTheApi()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new ValidateXmlInvoiceTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.ValidateXmlAsync(
            XmlInvoiceFormat.XRechnung, CancellationToken.None, xml: SampleUbl, rules: [ExtraRuleset.BrFr]);

        Assert.False(result.Valid);
        Assert.Equal("INPUT-RULES", Assert.Single(result.Errors!).Rule);
        Assert.Null(client.LastXmlFormat);
    }

    [Fact]
    public async Task ValidatePdf_RefusesRulesOnZugferd()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new ValidatePdfInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        var result = await tool.ValidatePdfAsync(
            PdfInvoiceFormat.Zugferd, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf), rules: [ExtraRuleset.BrFr]);

        Assert.False(result.Valid);
        Assert.Equal("INPUT-RULES", Assert.Single(result.Errors!).Rule);
        Assert.Null(client.LastPdfFormat);
    }

    [Fact]
    public async Task ValidatePdf_ForwardsRulesOnFacturX()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new ValidatePdfInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        await tool.ValidatePdfAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf), rules: [ExtraRuleset.BrFr]);

        Assert.Equal([ExtraRuleset.BrFr], client.LastRules);
    }

    [Fact]
    public async Task Embed_RefusesRulesOnZugferd()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmbedInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        var result = await tool.EmbedInvoiceAsync(
            PdfInvoiceFormat.Zugferd, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf), xml: SampleCii, rules: [ExtraRuleset.BrFr]);

        Assert.True(result.IsError);
        Assert.Contains("INPUT-RULES", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
        Assert.Null(client.LastEmbedFormat);
    }

    [Fact]
    public async Task Embed_ForwardsRulesOnFacturX()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmbedInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        var result = await tool.EmbedInvoiceAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf), xml: SampleCii, rules: [ExtraRuleset.BrFr]);

        Assert.False(result.IsError ?? false);
        Assert.Equal([ExtraRuleset.BrFr], client.LastRules);
    }

    [Fact]
    public async Task ValidationReport_RefusesRulesOnXRechnung()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new RenderValidationReportTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.RenderValidationReportAsync(
            ValidationReportFormat.XRechnung, CancellationToken.None, xml: SampleUbl, rules: [ExtraRuleset.BrFr]);

        Assert.True(result.IsError);
        Assert.Contains("INPUT-RULES", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
        Assert.Null(client.LastReportFormat);
    }

    [Fact]
    public async Task ValidationReport_ForwardsRulesAndFooterBrand()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new RenderValidationReportTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.RenderValidationReportAsync(
            ValidationReportFormat.Cii, CancellationToken.None, xml: SampleCii,
            rules: [ExtraRuleset.BrFr], footerBrand: FooterBrand.None);

        Assert.False(result.IsError ?? false);
        Assert.Equal([ExtraRuleset.BrFr], client.LastRules);
        Assert.Equal(FooterBrand.None, client.LastFooterBrand);
    }

    [Fact]
    public async Task Render_ForwardsLogoUrlAndFooterBrand()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new RenderInvoiceTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.RenderInvoiceAsync(
            XmlInvoiceFormat.Ubl, CancellationToken.None, xml: SampleUbl,
            logoUrl: "https://example.test/logo.png", footerBrand: FooterBrand.None);

        Assert.False(result.IsError ?? false);
        Assert.Equal("https://example.test/logo.png", client.LastLogoUrl);
        Assert.Equal(FooterBrand.None, client.LastFooterBrand);
    }

    [Fact]
    public async Task Convert_ForwardsFooterBrand()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new ConvertInvoiceTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.ConvertInvoiceAsync(
            InvoiceFormat.Ubl, InvoiceFormat.FacturX, CancellationToken.None, xml: SampleUbl, footerBrand: FooterBrand.None);

        Assert.False(result.IsError ?? false);
        Assert.Equal(FooterBrand.None, client.LastFooterBrand);
    }
}
