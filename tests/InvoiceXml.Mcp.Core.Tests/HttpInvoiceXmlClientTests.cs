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
            rules: null,
            CancellationToken.None);

        Assert.Equal("/v1/validate/xrechnung", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.True(result.Valid);
        Assert.Equal("UBL 2.1", result.Data?.ConformanceLevel);
    }

    [Fact]
    public async Task CreateInvoiceAsync_SerializesProfileSyntaxAndRulesOptions()
    {
        var responsePayload = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" />"u8.ToArray();
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, responsePayload, "application/xml", "invoice-7-ubl.xml"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.CreateInvoiceAsync(
            InvoiceFormat.Ubl,
            new InvoiceDocument { InvoiceNumber = "7", Currency = "EUR" },
            new CreateInvoiceOptions { Profile = "nlcius", Syntax = "cii", Rules = [ExtraRuleset.BrFr] },
            CancellationToken.None);

        Assert.Contains("\"profile\":\"nlcius\"", handler.LastRequestBody);
        Assert.Contains("\"syntax\":\"cii\"", handler.LastRequestBody);
        Assert.Contains("\"rules\":[\"br-fr\"]", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateInvoiceAsync_SerializesLogoUrlAndFooterBrandOptions()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, pdf, "application/pdf", "invoice-facturx.pdf"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.CreateInvoiceAsync(
            InvoiceFormat.FacturX,
            new InvoiceDocument { InvoiceNumber = "10", Currency = "EUR" },
            new CreateInvoiceOptions { LogoUrl = "https://example.test/logo.png", FooterBrand = FooterBrand.None },
            CancellationToken.None);

        Assert.Contains("\"logoUrl\":\"https://example.test/logo.png\"", handler.LastRequestBody);
        Assert.Contains("\"footerBrand\":\"none\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task CreateInvoiceAsync_SerializesExpandedModelWithApiWireNames()
    {
        var responsePayload = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" />"u8.ToArray();
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, responsePayload, "application/xml", "invoice-11-xrechnung.xml"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var invoice = new InvoiceDocument
        {
            InvoiceNumber = "11",
            Currency = "EUR",
            Notes = [new InvoiceNote { Note = "Recovery costs apply.", SubjectCode = "PMT" }],
            PrecedingInvoiceReferences = [new PrecedingInvoiceReference { Reference = "INV-1", IssueDate = new DateOnly(2026, 9, 1) }],
            Seller = new SellerParty
            {
                Name = "Seller GmbH",
                Contact = new PartyContact { Name = "Jo", Phone = "+49 30 1", Email = "jo@seller.test" },
                ElectronicAddress = new SchemeIdentifier { Identifier = "DE123456789", SchemeId = "9930" },
                LegalRegistration = new SchemeIdentifier { Identifier = "HRB 1" },
                TaxRegistrationIdentifier = "12/345/67890",
            },
            Buyer = new BuyerParty { Name = "Buyer AG", Identifiers = [new SchemeIdentifier { Identifier = "4000001000005", SchemeId = "0088" }] },
            Delivery = new DeliveryInformation { ActualDeliveryDate = new DateOnly(2026, 9, 30), DeliveryAddress = new DeliveryAddress { Country = "DE" } },
            InvoicingPeriod = new Period { StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 30) },
            PaymentDetails = new PaymentDetails { PaymentMeansCode = "58", PaymentAccountIdentifier = "DE02120300000000202051", PaymentTerms = "Net 30" },
            Allowances = [new DocumentAllowanceCharge { Amount = 5m, VatCategoryCode = VatCategoryCode.S, VatRate = 19m, Reason = "Discount" }],
            Charges = [new DocumentAllowanceCharge { Amount = 2m, VatCategoryCode = VatCategoryCode.S, VatRate = 19m, ReasonCode = "FC" }],
            SupportingDocuments = [new SupportingDocument { Reference = "TS-1", ExternalUri = "https://example.test/ts.pdf" }],
            Lines =
            [
                new InvoiceLine
                {
                    LineId = "1",
                    Quantity = 1m,
                    LinePeriod = new Period { StartDate = new DateOnly(2026, 9, 1) },
                    Allowances = [new LineAllowanceCharge { Amount = 1m, Reason = "Line discount" }],
                    Item = new ItemInformation
                    {
                        Name = "Consulting",
                        StandardIdentifier = new SchemeIdentifier { Identifier = "4012345678901", SchemeId = "0160" },
                        Classifications = [new ItemClassification { Identifier = "72000000", SchemeId = "STI" }],
                        Attributes = [new ItemProperty { Name = "Level", Value = "Senior" }],
                    },
                },
            ],
            Totals = new DocumentTotals { PaidAmount = 10m, RoundingAmount = 0.01m },
        };

        await client.CreateInvoiceAsync(InvoiceFormat.XRechnung, invoice, options: null, CancellationToken.None);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var doc = body.RootElement.GetProperty("invoice");

        // Every path below is the API InvoiceDocument's camelCase wire name.
        Assert.Equal("PMT", doc.GetProperty("notes")[0].GetProperty("subjectCode").GetString());
        Assert.Equal("Recovery costs apply.", doc.GetProperty("notes")[0].GetProperty("note").GetString());
        Assert.Equal("2026-09-01", doc.GetProperty("precedingInvoiceReferences")[0].GetProperty("issueDate").GetString());
        var seller = doc.GetProperty("seller");
        Assert.Equal("jo@seller.test", seller.GetProperty("contact").GetProperty("email").GetString());
        Assert.Equal("9930", seller.GetProperty("electronicAddress").GetProperty("schemeId").GetString());
        Assert.Equal("HRB 1", seller.GetProperty("legalRegistration").GetProperty("identifier").GetString());
        Assert.Equal("12/345/67890", seller.GetProperty("taxRegistrationIdentifier").GetString());
        Assert.Equal("0088", doc.GetProperty("buyer").GetProperty("identifiers")[0].GetProperty("schemeId").GetString());
        Assert.Equal("DE", doc.GetProperty("delivery").GetProperty("deliveryAddress").GetProperty("country").GetString());
        Assert.Equal("2026-09-30", doc.GetProperty("delivery").GetProperty("actualDeliveryDate").GetString());
        Assert.Equal("2026-09-30", doc.GetProperty("invoicingPeriod").GetProperty("endDate").GetString());
        var payment = doc.GetProperty("paymentDetails");
        Assert.Equal("58", payment.GetProperty("paymentMeansCode").GetString());
        Assert.Equal("DE02120300000000202051", payment.GetProperty("paymentAccountIdentifier").GetString());
        Assert.Equal("Net 30", payment.GetProperty("paymentTerms").GetString());
        Assert.Equal(5m, doc.GetProperty("allowances")[0].GetProperty("amount").GetDecimal());
        Assert.Equal(19m, doc.GetProperty("allowances")[0].GetProperty("vatRate").GetDecimal());
        Assert.True(doc.GetProperty("allowances")[0].TryGetProperty("vatCategoryCode", out _));
        Assert.Equal("FC", doc.GetProperty("charges")[0].GetProperty("reasonCode").GetString());
        Assert.Equal("https://example.test/ts.pdf", doc.GetProperty("supportingDocuments")[0].GetProperty("externalUri").GetString());
        var line = doc.GetProperty("lines")[0];
        Assert.Equal("2026-09-01", line.GetProperty("linePeriod").GetProperty("startDate").GetString());
        Assert.Equal(1m, line.GetProperty("allowances")[0].GetProperty("amount").GetDecimal());
        var item = line.GetProperty("item");
        Assert.Equal("0160", item.GetProperty("standardIdentifier").GetProperty("schemeId").GetString());
        Assert.Equal("STI", item.GetProperty("classifications")[0].GetProperty("schemeId").GetString());
        Assert.Equal("Senior", item.GetProperty("attributes")[0].GetProperty("value").GetString());
        Assert.Equal(10m, doc.GetProperty("totals").GetProperty("paidAmount").GetDecimal());
        Assert.Equal(0.01m, doc.GetProperty("totals").GetProperty("roundingAmount").GetDecimal());
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
        Assert.DoesNotContain("\"syntax\"", handler.LastRequestBody);
        Assert.DoesNotContain("\"rules\"", handler.LastRequestBody);
        Assert.DoesNotContain("\"pdfUrl\"", handler.LastRequestBody);
        Assert.DoesNotContain("\"logoUrl\"", handler.LastRequestBody);
        Assert.DoesNotContain("\"footerBrand\"", handler.LastRequestBody);
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

        var result = await client.ValidateXmlAsync(XmlInvoiceFormat.Ubl, "<Invoice/>", rules: null, CancellationToken.None);

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
            PdfInvoiceFormat.Zugferd, Array.Empty<byte>(), rules: null, CancellationToken.None));
    }

    [Fact]
    public async Task RenderToPdfAsync_PostsMultipartToRenderRoute()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, pdf, "application/pdf", "rendered.pdf"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        var result = await client.RenderToPdfAsync(
            XmlInvoiceFormat.XRechnung, "<Invoice/>", PdfLanguage.DE, logoUrl: null, footerBrand: null, CancellationToken.None);

        Assert.Equal("/v1/render/xrechnung/to/pdf", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.StartsWith("multipart/form-data", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("language", handler.LastRequestBody); // the language form field is present
        Assert.DoesNotContain("logoUrl", handler.LastRequestBody);
        Assert.DoesNotContain("footerBrand", handler.LastRequestBody);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("rendered.pdf", result.FileName);
    }

    [Fact]
    public async Task RenderToPdfAsync_SendsLogoUrlAndFooterBrandFormFields()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, [0x25, 0x50, 0x44, 0x46], "application/pdf", "rendered.pdf"));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.RenderToPdfAsync(
            XmlInvoiceFormat.Ubl, "<Invoice/>", PdfLanguage.EN,
            logoUrl: "https://example.test/logo.png", footerBrand: FooterBrand.None, CancellationToken.None);

        Assert.Contains("name=logoUrl", handler.LastRequestBody);
        Assert.Contains("https://example.test/logo.png", handler.LastRequestBody);
        Assert.Contains("name=footerBrand", handler.LastRequestBody);
        Assert.Contains("none", handler.LastRequestBody);
    }

    [Fact]
    public async Task ValidateXmlAsync_SendsEachRuleAsItsOwnFormPart()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK, """{"valid":true}"""));
        var client = new HttpInvoiceXmlClient(BuildClient(handler));

        await client.ValidateXmlAsync(
            XmlInvoiceFormat.Cii, "<x/>", [ExtraRuleset.BrFr, ExtraRuleset.BrFr], CancellationToken.None);

        // Duplicates collapse to one part; the API binds 'rules' as a string array.
        var body = handler.LastRequestBody!;
        Assert.Equal(1, CountOccurrences(body, "name=rules"));
        Assert.Contains("br-fr", body);
    }

    [Fact]
    public async Task ConvertAsync_SendsFooterBrandOnlyForHybridTargets()
    {
        var pdfHandler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, [0x25, 0x50, 0x44, 0x46], "application/pdf", "invoice-facturx.pdf"));
        await new HttpInvoiceXmlClient(BuildClient(pdfHandler)).ConvertAsync(
            InvoiceFormat.Ubl, InvoiceFormat.FacturX, "<Invoice/>"u8.ToArray(), "application/xml", "invoice.xml",
            FooterBrand.None, CancellationToken.None);
        Assert.Contains("name=footerBrand", pdfHandler.LastRequestBody);

        var xmlHandler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(
            HttpStatusCode.OK, "<x/>"u8.ToArray(), "application/xml", "invoice-cii.xml"));
        await new HttpInvoiceXmlClient(BuildClient(xmlHandler)).ConvertAsync(
            InvoiceFormat.Ubl, InvoiceFormat.Cii, "<Invoice/>"u8.ToArray(), "application/xml", "invoice.xml",
            FooterBrand.None, CancellationToken.None);
        Assert.DoesNotContain("footerBrand", xmlHandler.LastRequestBody);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    [Fact]
    public async Task ExtractAsync_PostsToTargetSlugRoute()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(
            HttpStatusCode.OK, """{"invoice":{"invoiceNumber":"1"}}"""));
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
            ValidationReportFormat.XRechnung,
            System.Text.Encoding.UTF8.GetBytes("<Invoice/>"),
            "application/xml",
            "invoice.xml",
            rules: null,
            footerBrand: null,
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
            ValidationReportFormat.FacturX, pdf, "application/pdf", "invoice.pdf", rules: null, footerBrand: null, CancellationToken.None);

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

        await client.EmbedAsync(PdfInvoiceFormat.FacturX, pdf, "<CrossIndustryInvoice/>", rules: null, CancellationToken.None);

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
            InvoiceFormat.Cii, InvoiceFormat.Ubl, xml, "application/xml", "invoice.xml", footerBrand: null, CancellationToken.None);

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
            client.ValidateXmlAsync(XmlInvoiceFormat.Ubl, "<x/>", rules: null, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("Invalid or inactive API key", ex.ResponseBody);
    }
}
