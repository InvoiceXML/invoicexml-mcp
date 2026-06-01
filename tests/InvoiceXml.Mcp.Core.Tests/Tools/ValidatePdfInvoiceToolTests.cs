using System.Net;
using System.Text;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class ValidatePdfInvoiceToolTests
{
    // A minimal *complete* PDF: %PDF header + %%EOF trailer. Passes the completeness check.
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    private static ValidatePdfInvoiceTool Build(
        CapturingInvoiceXmlClient? client = null,
        IRemoteFileFetcher? fetcher = null)
        => new(
            client ?? new CapturingInvoiceXmlClient(),
            fetcher ?? new FakeRemoteFileFetcher(SamplePdf));

    [Fact]
    public async Task Base64Mode_ForwardsBytesToClient()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        await tool.ValidatePdfAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.Equal(PdfInvoiceFormat.FacturX, client.LastPdfFormat);
        Assert.Equal(SamplePdf, client.LastPdfBytes);
    }

    [Fact]
    public async Task InvalidBase64_ReturnsStructuredFailureNotException()
    {
        var tool = Build();

        var result = await tool.ValidatePdfAsync(
            PdfInvoiceFormat.Zugferd, CancellationToken.None,
            pdfBase64: "not-base64-!@#$");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors!, e => e.Rule == "INPUT-BASE64");
    }

    [Fact]
    public async Task UrlMode_FetchesAndForwardsBytes()
    {
        var client = new CapturingInvoiceXmlClient();
        var fetcher = new FakeRemoteFileFetcher(SamplePdf);
        var tool = Build(client, fetcher);

        await tool.ValidatePdfAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfUrl: "https://example.com/invoice.pdf");

        Assert.Equal("https://example.com/invoice.pdf", fetcher.LastUrl);
        Assert.Equal(SamplePdf, client.LastPdfBytes);
    }

    [Fact]
    public async Task UrlMode_FetchFailure_ReturnsInputUrlError()
    {
        var fetcher = new FakeRemoteFileFetcher(
            new FileFetchException(FileFetchError.BlockedHost, "blocked"));
        var tool = Build(fetcher: fetcher);

        var result = await tool.ValidatePdfAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfUrl: "https://169.254.169.254/x.pdf");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors!, e => e.Rule == "INPUT-URL");
    }

    [Fact]
    public async Task NoInput_ReturnsInputRequired()
    {
        var tool = Build();

        var result = await tool.ValidatePdfAsync(PdfInvoiceFormat.FacturX, CancellationToken.None);

        Assert.False(result.Valid);
        Assert.Contains(result.Errors!, e => e.Rule == "INPUT-REQUIRED");
    }

    [Fact]
    public async Task MultipleInputs_ReturnsInputExclusive()
    {
        var tool = Build();

        var result = await tool.ValidatePdfAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf),
            pdfUrl: "https://example.com/invoice.pdf");

        Assert.False(result.Valid);
        Assert.Contains(result.Errors!, e => e.Rule == "INPUT-EXCLUSIVE");
    }

    [Fact]
    public async Task IncompletePdf_RejectedBeforeApiCall()
    {
        // %PDF header but no %%EOF — the fabricated/truncated case from the ChatGPT bug.
        var incomplete = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <<>> endobj");
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client);

        var result = await tool.ValidatePdfAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(incomplete));

        Assert.False(result.Valid);
        Assert.Contains(result.Errors!, e => e.Rule == "INPUT-INCOMPLETE-PDF");
        // Critically: the API client must NOT have been called (no credit spent).
        Assert.Null(client.LastPdfBytes);
    }

    [Fact]
    public async Task NonPdfBytes_PassThroughToClient()
    {
        // Doesn't start with %PDF → the PDF completeness check is skipped; the API decides.
        var notPdf = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><Invoice/>");
        var client = new CapturingInvoiceXmlClient();
        var tool = Build(client, new FakeRemoteFileFetcher(notPdf));

        await tool.ValidatePdfAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(notPdf));

        Assert.Equal(notPdf, client.LastPdfBytes);
    }

    [Fact]
    public async Task ApiUnauthorized_SynthesizesFailureResultNotException()
    {
        var client = new ThrowingValidationClient(
            new InvoiceXmlApiException(HttpStatusCode.Unauthorized, """{"detail":"bad key"}"""));
        var tool = new ValidatePdfInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        var result = await tool.ValidatePdfAsync(
            PdfInvoiceFormat.FacturX, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.False(result.Valid);
        Assert.NotNull(result.Errors);
    }

    private sealed class ThrowingValidationClient(Exception ex) : Interfaces.IInvoiceXmlClient
    {
        public Task<Models.CreateInvoiceResult> CreateInvoiceAsync(
            InvoiceFormat format, Models.InvoiceDocument invoice, Models.PdfRenderOptions? options, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Models.ValidationResult> ValidateXmlAsync(
            XmlInvoiceFormat format, string xml, CancellationToken ct = default)
            => Task.FromException<Models.ValidationResult>(ex);

        public Task<Models.ValidationResult> ValidatePdfAsync(
            PdfInvoiceFormat format, byte[] pdf, CancellationToken ct = default)
            => Task.FromException<Models.ValidationResult>(ex);

        public Task<Models.DocumentArtifact> RenderToPdfAsync(
            XmlInvoiceFormat format, string xml, PdfLanguage language, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Models.DocumentArtifact> ExtractAsync(
            ExtractTarget target, byte[] pdf, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Models.DocumentArtifact> EmbedAsync(
            PdfInvoiceFormat format, byte[] pdf, string ciiXml, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Models.DocumentArtifact> ConvertAsync(
            InvoiceFormat source, InvoiceFormat target, byte[] content, string contentType, string fileName, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Models.DocumentArtifact> TransformAsync(
            InvoiceFormat target, byte[] pdf, PdfLanguage language, string? buyerReference, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
