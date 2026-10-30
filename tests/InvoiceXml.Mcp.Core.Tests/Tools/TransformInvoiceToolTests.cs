using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

/// <summary>
/// The transform tool's own fields (language, preserveOriginalPdf, includeAdvancedProperties,
/// buyerReference) and the review reminder every success carries, since the data behind a
/// transform is an AI read of the PDF.
/// </summary>
public class TransformInvoiceToolTests
{
    // A minimal complete PDF: %PDF header + %%EOF trailer.
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    private static readonly DocumentArtifact XmlArtifact = new()
    {
        Content = Encoding.UTF8.GetBytes("<CrossIndustryInvoice/>"),
        ContentType = "application/xml",
        FileName = "invoice-cii.xml",
    };

    private static TransformInvoiceTool Build(CapturingInvoiceXmlClient? client = null)
        => new(client ?? new CapturingInvoiceXmlClient(), new FakeRemoteFileFetcher(SamplePdf));

    // The defaults: detected language, the uploaded pages not kept, and the advanced-details
    // section on, so the user can check the embedded XML on the rendered pages.
    [Fact]
    public async Task HybridTarget_LeavesLanguageToTheDetection_PreserveOff_AndAdvancedPropertiesOn_ByDefault()
    {
        var client = new CapturingInvoiceXmlClient();

        var result = await Build(client).TransformInvoiceAsync(
            InvoiceFormat.Zugferd, CancellationToken.None, pdfUrl: "https://example.com/invoice.pdf");

        Assert.False(result.IsError ?? false);
        Assert.Equal(InvoiceFormat.Zugferd, client.LastTransformTarget);
        Assert.Equal(SamplePdf, client.LastTransformPdf);
        Assert.Null(client.LastTransformLanguage);
        Assert.False(client.LastTransformPreserveOriginalPdf);
        Assert.True(client.LastTransformIncludeAdvancedProperties);
    }

    [Fact]
    public async Task HybridTarget_ForwardsLanguagePreserveOriginalPdfAndAdvancedPropertiesOff()
    {
        var client = new CapturingInvoiceXmlClient();

        await Build(client).TransformInvoiceAsync(
            InvoiceFormat.FacturX, CancellationToken.None, pdfBase64: Convert.ToBase64String(SamplePdf),
            language: PdfLanguage.FR, preserveOriginalPdf: true, includeAdvancedProperties: false);

        Assert.Equal(PdfLanguage.FR, client.LastTransformLanguage);
        Assert.True(client.LastTransformPreserveOriginalPdf);
        Assert.False(client.LastTransformIncludeAdvancedProperties);
    }

    [Fact]
    public async Task XRechnungTarget_ForwardsBuyerReference()
    {
        var client = new CapturingInvoiceXmlClient(artifact: XmlArtifact);

        await Build(client).TransformInvoiceAsync(
            InvoiceFormat.XRechnung, CancellationToken.None, pdfUrl: "https://example.com/invoice.pdf",
            buyerReference: "04011000-12345-67");

        Assert.Equal("04011000-12345-67", client.LastTransformBuyerReference);
    }

    [Fact]
    public async Task PdfTarget_DeliversAnAttachment_AndRemindsTheAgentToWarnTheUser()
    {
        var result = await Build().TransformInvoiceAsync(
            InvoiceFormat.Zugferd, CancellationToken.None, pdfUrl: "https://example.com/invoice.pdf");

        Assert.Equal(2, result.Content.Count);
        var summary = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
        Assert.Contains("Electronic invoice details", summary.Text);
        Assert.Contains("doubles as the PDF preview", summary.Text);
        Assert.EndsWith(TransformInvoiceTool.ReviewReminder, summary.Text);
    }

    [Fact]
    public async Task PdfTarget_WithoutAdvancedProperties_DoesNotPromiseTheDetailsSection()
    {
        var result = await Build().TransformInvoiceAsync(
            InvoiceFormat.Zugferd, CancellationToken.None, pdfUrl: "https://example.com/invoice.pdf",
            includeAdvancedProperties: false);

        var summary = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.DoesNotContain("Electronic invoice details", summary.Text);
        Assert.Contains("doubles as the PDF preview", summary.Text);
        Assert.EndsWith(TransformInvoiceTool.ReviewReminder, summary.Text);
    }

    [Fact]
    public async Task PreservedPdf_SaysThePagesAreTheUsersOwn()
    {
        var result = await Build().TransformInvoiceAsync(
            InvoiceFormat.FacturX, CancellationToken.None, pdfUrl: "https://example.com/invoice.pdf",
            preserveOriginalPdf: true);

        var summary = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.Contains("embedded XML only", summary.Text);
        Assert.EndsWith(TransformInvoiceTool.ReviewReminder, summary.Text);
    }

    [Fact]
    public async Task XmlTarget_DeliversTheXmlInline_AndRemindsTheAgentToWarnTheUser()
    {
        var result = await Build(new CapturingInvoiceXmlClient(artifact: XmlArtifact)).TransformInvoiceAsync(
            InvoiceFormat.Cii, CancellationToken.None, pdfUrl: "https://example.com/invoice.pdf");

        Assert.Equal(2, result.Content.Count);
        var summary = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.EndsWith(TransformInvoiceTool.ReviewReminder, summary.Text);
        Assert.Contains("<CrossIndustryInvoice/>", Assert.IsType<TextContentBlock>(result.Content[1]).Text);
    }

    [Fact]
    public async Task IncompletePdf_RejectedBeforeApiCall()
    {
        var incomplete = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <<>> endobj");
        var client = new CapturingInvoiceXmlClient();

        var result = await Build(client).TransformInvoiceAsync(
            InvoiceFormat.Ubl, CancellationToken.None, pdfBase64: Convert.ToBase64String(incomplete));

        Assert.True(result.IsError);
        Assert.Contains("INPUT-INCOMPLETE-PDF", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Null(client.LastTransformPdf); // API not called, no credit spent
    }
}
