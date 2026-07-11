using System.Net;
using System.Text.Json;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;

namespace InvoiceXml.Mcp.Core.Tests;

public class HttpInvoiceXmlClientTests
{
    private static HttpClient BuildClient(StubHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.invoicexml.test") };

    [Fact]
    public async Task CreateInvoiceAsync_PostsJsonToFormatSlugRoute()
    {
        var responsePayload = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" />"u8.ToArray();
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, responsePayload, "application/xml", "invoice-42-ubl.xml"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.CreateInvoiceAsync(
            InvoiceFormat.Ubl,
            new InvoiceDocument { InvoiceNumber = "42", Currency = "EUR" },
            options: null,
            CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/v1/create/ubl", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("\"invoiceNumber\":\"42\"", handler.LastRequestBody);

        Assert.Equal(responsePayload, result.Content);
        Assert.Equal("application/xml", result.ContentType);
        Assert.Equal("invoice-42-ubl.xml", result.FileName);
    }

    [Fact]
    public async Task CreateInvoiceAsync_SendsBaseRouteForFacturX()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, pdf, "application/pdf", "invoice-factur-x.pdf"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.CreateInvoiceAsync(
            InvoiceFormat.FacturX,
            new InvoiceDocument { InvoiceNumber = "1", Currency = "EUR" },
            new CreateInvoiceOptions { Language = PdfLanguage.DE, BrandColor = "#1F4E79" },
            CancellationToken.None);

        Assert.Equal("/v1/create/facturx", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"language\":\"DE\"", handler.LastRequestBody);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(pdf, result.Content);
    }

    [Fact]
    public async Task ValidateXmlAsync_PostsMultipartToValidateRoute()
    {
        var json = """{"valid":true,"detail":"ok","data":{"schemaValid":true,"schematronValid":true,"conformanceLevel":"UBL 2.1"},"errors":[],"warnings":[]}""";
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.ValidateXmlAsync(
            XmlInvoiceFormat.XRechnung,
            "<Invoice/>",
            CancellationToken.None);

        Assert.Equal("/v1/validate/xrechnung", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.True(result.Valid);
        Assert.Equal("UBL 2.1", result.Data?.ConformanceLevel);
    }

    [Fact]
    public async Task CreateInvoiceAsync_SerializesProfileAndVersionOptions()
    {
        var responsePayload = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" />"u8.ToArray();
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, responsePayload, "application/xml", "invoice-7-ubl.xml"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.CreateInvoiceAsync(
            InvoiceFormat.Ubl,
            new InvoiceDocument { InvoiceNumber = "7", Currency = "EUR" },
            new CreateInvoiceOptions { Profile = "nlcius", Version = "3.0" },
            CancellationToken.None);

        Assert.Contains("\"profile\":\"nlcius\"", handler.LastRequestBody);
        Assert.Contains("\"version\":\"3.0\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateInvoiceAsync_SerializesIncludeAdvancedPropertiesOption()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, pdf, "application/pdf", "invoice-facturx.pdf"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.CreateInvoiceAsync(
            InvoiceFormat.FacturX,
            new InvoiceDocument { InvoiceNumber = "9", Currency = "EUR" },
            new CreateInvoiceOptions { IncludeAdvancedProperties = true },
            CancellationToken.None);

        Assert.Contains("\"includeAdvancedProperties\":true", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateInvoiceAsync_OmitsUnsetOptionsFromTheWire()
    {
        var responsePayload = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" />"u8.ToArray();
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, responsePayload, "application/xml", "invoice-8-ubl.xml"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.CreateInvoiceAsync(
            InvoiceFormat.Ubl,
            new InvoiceDocument { InvoiceNumber = "8", Currency = "EUR" },
            options: null,
            CancellationToken.None);

        // Null-valued options must not appear on the wire, so the API's own
        // defaulting (e.g. profile peppol-bis-3) stays in charge.
        Assert.DoesNotContain("\"profile\"", handler.LastRequestBody);
        Assert.DoesNotContain("\"version\"", handler.LastRequestBody);
        Assert.DoesNotContain("\"pdfUrl\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task ValidateXmlAsync_ParsesProfileCustomizationIdAndLayer()
    {
        var json = """
        {
          "valid": true,
          "detail": "Your invoice is UBL 2.1 compliant and meets the Peppol BIS Billing 3.0 rules.",
          "data": {
            "schemaValid": true,
            "schematronValid": true,
            "conformanceLevel": "UBL 2.1",
            "profile": "peppol-bis-3",
            "customizationId": "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0"
          },
          "errors": [],
          "warnings": [
            {
              "rule": "PROFILE-DETECTION",
              "layer": "cius",
              "line": null,
              "message": "advisory",
              "btCodes": [],
              "fields": [],
              "raw": "[PROFILE-DETECTION] CIUS: advisory"
            }
          ]
        }
        """;
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.ValidateXmlAsync(XmlInvoiceFormat.Ubl, "<Invoice/>", CancellationToken.None);

        Assert.Equal("peppol-bis-3", result.Data?.Profile);
        Assert.Contains("poacc:billing:3.0", result.Data?.CustomizationId);
        var warning = Assert.Single(result.Warnings!);
        Assert.Equal("cius", warning.Layer);
        Assert.Equal("PROFILE-DETECTION", warning.Rule);
    }

    [Fact]
    public async Task ValidatePdfAsync_RejectsEmptyBuffer()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await Assert.ThrowsAsync<ArgumentException>(() => client.ValidatePdfAsync(
            PdfInvoiceFormat.Zugferd, Array.Empty<byte>(), CancellationToken.None));
    }

    [Fact]
    public async Task RenderToPdfAsync_PostsMultipartToRenderRoute()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, pdf, "application/pdf", "rendered.pdf"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.RenderToPdfAsync(
            XmlInvoiceFormat.XRechnung, "<Invoice/>", PdfLanguage.DE, CancellationToken.None);

        Assert.Equal("/v1/render/xrechnung/to/pdf", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("language", handler.LastRequestBody); // the language form field is present
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("rendered.pdf", result.FileName);
    }

    [Fact]
    public async Task ExtractAsync_PostsToTargetSlugRoute()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(
            HttpStatusCode.OK, """{"invoiceNumber":"1"}"""));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.ExtractAsync(
            ExtractTarget.Json, new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf", "invoice.pdf", CancellationToken.None);

        Assert.Equal("/v1/extract/json", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("application/json", result.ContentType);
    }

    [Fact]
    public async Task ExtractAsync_AttachmentsTarget_PostsToAttachmentsRouteAndDefaultsZipName()
    {
        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // "PK.."
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, zip, "application/zip"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.ExtractAsync(
            ExtractTarget.Attachments, new byte[] { 0x25, 0x50, 0x44, 0x46 }, "application/pdf", "invoice.pdf", CancellationToken.None);

        Assert.Equal("/v1/extract/attachments", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("application/zip", result.ContentType);
        // No Content-Disposition on the stub, so the client's default applies.
        Assert.Equal("invoice-attachments.zip", result.FileName);
        Assert.Equal(zip, result.Content);
    }

    [Fact]
    public async Task ExtractAsync_AttachmentsTarget_AcceptsXmlUpload()
    {
        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, zip, "application/zip"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.ExtractAsync(
            ExtractTarget.Attachments,
            System.Text.Encoding.UTF8.GetBytes("<CrossIndustryInvoice/>"),
            "application/xml",
            "invoice.xml",
            CancellationToken.None);

        Assert.Equal("/v1/extract/attachments", handler.LastRequest!.RequestUri!.AbsolutePath);
        // The XML file part signals the API to take the plain-XML path.
        Assert.Contains("application/xml", handler.LastRequestBody);
        Assert.Contains("invoice.xml", handler.LastRequestBody);
    }

    [Fact]
    public async Task ValidationReportPdfAsync_PostsToReportRouteAndReadsVerdictHeader()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var response = StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, pdf, "application/pdf", "invoice-report.pdf");
        response.Headers.Add("X-Invoice-Valid", "false");
        var handler = new StubHttpMessageHandler(response);
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.ValidationReportPdfAsync(
            InvoiceFormat.XRechnung,
            System.Text.Encoding.UTF8.GetBytes("<Invoice/>"),
            "application/xml",
            "invoice.xml",
            CancellationToken.None);

        Assert.Equal("/v1/validate/xrechnung/report", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.False(result.Valid);
        Assert.Equal("invoice-report.pdf", result.Report.FileName);
        Assert.Equal(pdf, result.Report.Content);
    }

    [Fact]
    public async Task ValidationReportPdfAsync_MissingVerdictHeader_YieldsNullValid()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, pdf, "application/pdf"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.ValidationReportPdfAsync(
            InvoiceFormat.FacturX, pdf, "application/pdf", "invoice.pdf", CancellationToken.None);

        Assert.Equal("/v1/validate/facturx/report", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Null(result.Valid);
        Assert.Equal("validation-report-facturx.pdf", result.Report.FileName);
    }

    [Fact]
    public async Task EmbedAsync_PostsPdfAndXmlPartsToEmbedRoute()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, pdf, "application/pdf", "invoice-facturx.pdf"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.EmbedAsync(PdfInvoiceFormat.FacturX, pdf, "<CrossIndustryInvoice/>", CancellationToken.None);

        Assert.Equal("/v1/embed/facturx", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        // Two file parts: the PDF and the CII XML.
        Assert.Contains("application/pdf", handler.LastRequestBody);
        Assert.Contains("application/xml", handler.LastRequestBody);
    }

    [Fact]
    public async Task ConvertAsync_PostsToSourceTargetRoute()
    {
        var xml = "<CrossIndustryInvoice/>"u8.ToArray();
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, xml, "application/xml", "invoice-ubl.xml"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.ConvertAsync(
            InvoiceFormat.Cii, InvoiceFormat.Ubl, xml, "application/xml", "invoice.xml", CancellationToken.None);

        Assert.Equal("/v1/convert/cii/to/ubl", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("application/xml", result.ContentType);
        Assert.Equal("invoice-ubl.xml", result.FileName);
    }

    [Fact]
    public async Task NonSuccessStatus_RaisesInvoiceXmlApiException()
    {
        var problemJson = """{"title":"Unauthorized","status":401,"detail":"Invalid or inactive API key."}""";
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.Json(HttpStatusCode.Unauthorized, problemJson));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() =>
            client.ValidateXmlAsync(XmlInvoiceFormat.Ubl, "<x/>", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("Invalid or inactive API key", ex.ResponseBody);
    }
}
