using System.Net;
using System.Text;
using System.Text.Json;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

/// <summary>
/// The Order-X tools (<c>create_order</c>, <c>validate_order</c>, the
/// <c>order-x</c> report) and <c>get_account</c>, at both the client wire
/// level and the tool level.
/// </summary>
public class OrderAndAccountToolTests
{
    private const string SampleCio = "<rsm:SCRDMCCBDACIOMessageStructure xmlns:rsm=\"urn:un:unece:uncefact:data:SCRDMCCBDACIOMessageStructure:100\"/>";
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    private static HttpClient BuildHttp(StubHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.invoicexml.test") };

    [Theory]
    [InlineData(OrderFormat.OrderX, "/v1/create/order-x")]
    [InlineData(OrderFormat.Cio, "/v1/create/cio")]
    public async Task CreateOrderAsync_PostsOrderEnvelopeToFormatRoute(OrderFormat format, string expectedPath)
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, SamplePdf, "application/pdf", "order-x.pdf"));
        var client = new HttpInvoiceXmlClient(BuildHttp(handler));

        await client.CreateOrderAsync(
            format,
            new InvoiceDocument { InvoiceNumber = "PO-1", Currency = "EUR" },
            new CreateOrderOptions { Profile = OrderProfile.Extended, TypeCode = OrderTypeCode.OrderChange, FooterBrand = FooterBrand.None },
            CancellationToken.None);

        Assert.Equal(expectedPath, handler.LastRequest!.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("PO-1", body.RootElement.GetProperty("order").GetProperty("invoiceNumber").GetString());
        Assert.False(body.RootElement.TryGetProperty("invoice", out _));
        var options = body.RootElement.GetProperty("options");
        Assert.Equal("extended", options.GetProperty("profile").GetString());
        Assert.Equal("230", options.GetProperty("typeCode").GetString());
        Assert.Equal("none", options.GetProperty("footerBrand").GetString());
    }

    [Fact]
    public async Task ValidateOrderAsync_PostsToOrderXValidateRoute()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"valid":true}"""));
        var client = new HttpInvoiceXmlClient(BuildHttp(handler));

        var result = await client.ValidateOrderAsync(Encoding.UTF8.GetBytes(SampleCio), "application/xml", "order.xml", CancellationToken.None);

        Assert.True(result.Valid);
        Assert.Equal("/v1/validate/order-x", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("name=rules", handler.LastRequestBody);
    }

    [Fact]
    public async Task ValidationReportPdfAsync_OrderX_PostsToHyphenatedReportRoute()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, SamplePdf, "application/pdf", "report.pdf"));
        var client = new HttpInvoiceXmlClient(BuildHttp(handler));

        await client.ValidationReportPdfAsync(
            ValidationReportFormat.OrderX, SamplePdf, "application/pdf", "order.pdf",
            rules: null, footerBrand: null, CancellationToken.None);

        Assert.Equal("/v1/validate/order-x/report", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetAccountAsync_GetsMeAndParsesBalance()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"id":7,"plan":"free","creditsRemaining":42,"creditsTotal":50,"creditsConsumed":8,"creditsConsumedTotal":108}"""));
        var client = new HttpInvoiceXmlClient(BuildHttp(handler));

        var account = await client.GetAccountAsync(CancellationToken.None);

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/v1/me", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("free", account.Plan);
        Assert.Equal(42, account.CreditsRemaining);
        Assert.Equal(50, account.CreditsTotal);
        Assert.Equal(108, account.CreditsConsumedTotal);
    }

    [Fact]
    public async Task CreateOrderTool_OrderX_ReturnsPdfAsEmbeddedResource()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new CreateOrderTool(client);

        var result = await tool.CreateOrderAsync(
            OrderFormat.OrderX, new InvoiceDocument { InvoiceNumber = "PO-1", Currency = "EUR" },
            new CreateOrderOptions { TypeCode = OrderTypeCode.OrderResponse }, CancellationToken.None);

        Assert.False(result.IsError ?? false);
        Assert.Contains("Order-X", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
        Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
        Assert.Equal(OrderFormat.OrderX, client.LastOrderFormat);
        Assert.Equal(OrderTypeCode.OrderResponse, client.LastOrderOptions!.TypeCode);
    }

    [Fact]
    public async Task ValidateOrderTool_XmlInput_UploadsAsXml()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new ValidateOrderTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.ValidateOrderAsync(CancellationToken.None, xml: SampleCio);

        Assert.True(result.Valid);
        Assert.Equal("application/xml", client.LastOrderValidationContentType);
        Assert.Equal(SampleCio, Encoding.UTF8.GetString(client.LastOrderValidationContent!));
    }

    [Fact]
    public async Task ValidateOrderTool_PdfUrl_UploadsAsPdf()
    {
        var client = new CapturingInvoiceXmlClient();
        var fetcher = new FakeRemoteFileFetcher(SamplePdf);
        var tool = new ValidateOrderTool(client, fetcher);

        await tool.ValidateOrderAsync(CancellationToken.None, pdfUrl: "https://example.test/order.pdf");

        Assert.Equal("https://example.test/order.pdf", fetcher.LastUrl);
        Assert.Equal("application/pdf", client.LastOrderValidationContentType);
        Assert.Equal(SamplePdf, client.LastOrderValidationContent);
    }

    [Fact]
    public async Task ValidateOrderTool_TwoInputs_ReturnsInputErrorWithoutCallingTheApi()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new ValidateOrderTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.ValidateOrderAsync(
            CancellationToken.None, xml: SampleCio, pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.False(result.Valid);
        Assert.StartsWith("INPUT-", Assert.Single(result.Errors!).Rule);
        Assert.Null(client.LastOrderValidationContentType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidationReportTool_OrderX_AcceptsXmlOrPdf(bool asXml)
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new RenderValidationReportTool(client, new FakeRemoteFileFetcher([]));

        var result = asXml
            ? await tool.RenderValidationReportAsync(ValidationReportFormat.OrderX, CancellationToken.None, xml: SampleCio)
            : await tool.RenderValidationReportAsync(ValidationReportFormat.OrderX, CancellationToken.None, pdfBase64: Convert.ToBase64String(SamplePdf));

        Assert.False(result.IsError ?? false);
        Assert.Equal(ValidationReportFormat.OrderX, client.LastReportFormat);
        Assert.Equal(asXml ? "application/xml" : "application/pdf", client.LastReportContentType);
        // The API prints the upload name into the report, so an order must not be called an invoice.
        Assert.Equal(asXml ? "order.xml" : "order.pdf", client.LastReportFileName);
    }

    [Fact]
    public async Task ValidationReportTool_OrderX_RefusesRules()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new RenderValidationReportTool(client, new FakeRemoteFileFetcher([]));

        var result = await tool.RenderValidationReportAsync(
            ValidationReportFormat.OrderX, CancellationToken.None, xml: SampleCio, rules: [ExtraRuleset.BrFr]);

        Assert.True(result.IsError);
        Assert.Contains("INPUT-RULES", Assert.IsType<TextContentBlock>(result.Content[0]).Text);
    }

    [Fact]
    public async Task GetAccountTool_SummarisesBalanceAndReturnsJson()
    {
        var tool = new GetAccountTool(new CapturingInvoiceXmlClient());

        var result = await tool.GetAccountAsync(CancellationToken.None);

        Assert.False(result.IsError ?? false);
        var summary = Assert.IsType<TextContentBlock>(result.Content[0]).Text;
        Assert.Contains("subscription", summary);
        Assert.Contains("1,234", summary);
        Assert.Contains("5,000", summary);
        using var json = JsonDocument.Parse(Assert.IsType<TextContentBlock>(result.Content[1]).Text);
        Assert.Equal(1234, json.RootElement.GetProperty("creditsRemaining").GetInt32());
    }

    [Fact]
    public void CreateOrder_SchemaExposesSharedDocumentModelAndOrderOptions()
    {
        var schema = ToolSchemaTests.InputSchema<CreateOrderTool>(nameof(CreateOrderTool.CreateOrderAsync));

        Assert.True(ToolSchemaTests.TryResolve(schema, "order.paymentDetails.paymentAccountIdentifier", out _));
        Assert.True(ToolSchemaTests.TryResolve(schema, "options.typeCode", out var typeCode));
        Assert.Equal(["220", "230", "231"], ToolSchemaTests.EnumValues(typeCode).Order().ToArray());
        Assert.True(ToolSchemaTests.TryResolve(schema, "format", out var format));
        Assert.Equal(["cio", "order-x"], ToolSchemaTests.EnumValues(format).Order().ToArray());
    }

    [Fact]
    public void ValidationReport_SchemaOffersOrderX()
    {
        var schema = ToolSchemaTests.InputSchema<RenderValidationReportTool>(nameof(RenderValidationReportTool.RenderValidationReportAsync));

        Assert.True(ToolSchemaTests.TryResolve(schema, "format", out var format));
        Assert.Contains("order-x", ToolSchemaTests.EnumValues(format));
    }
}
