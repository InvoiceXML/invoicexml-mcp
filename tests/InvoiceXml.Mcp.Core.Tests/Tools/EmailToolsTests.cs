using System.Net;
using System.Text;
using System.Text.Json;
using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

/// <summary>
/// The email tools (<c>email_invoice</c>, <c>email_embedded_invoice</c>), at both the
/// client wire level and the tool level.
/// </summary>
public class EmailToolsTests
{
    private const string SampleCii = "<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\"/>";
    private const string Accepted = "{\"status\":\"accepted\",\"messageId\":\"0100019a-abc\"}";
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");

    private static readonly EmailMessage Email = new()
    {
        From = "invoices@acme.example",
        FromName = "Acme Billing",
        To = "ap@customer.example",
        Cc = ["books@acme.example", "tax@acme.example"],
        Bcc = ["archive@acme.example"],
        ReplyTo = "billing@acme.example",
        Subject = "Invoice 42",
        Message = "Hello",
    };

    private static HttpClient BuildHttp(StubHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://api.invoicexml.test") };

    [Theory]
    [InlineData(InvoiceFormat.Zugferd, "/v1/create/zugferd/email")]
    [InlineData(InvoiceFormat.FacturX, "/v1/create/facturx/email")]
    [InlineData(InvoiceFormat.XRechnung, "/v1/create/xrechnung/email")]
    [InlineData(InvoiceFormat.Ubl, "/v1/create/ubl/email")]
    [InlineData(InvoiceFormat.Cii, "/v1/create/cii/email")]
    public async Task EmailInvoiceAsync_PostsInvoiceOptionsAndEmailToTheFormatsEmailRoute(InvoiceFormat format, string expectedPath)
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK, Accepted));
        var client = new HttpInvoiceXmlClient(BuildHttp(handler));

        var result = await client.EmailInvoiceAsync(
            format,
            new InvoiceDocument { InvoiceNumber = "42", Currency = "EUR" },
            new CreateInvoiceOptions { FooterBrand = FooterBrand.None },
            Email,
            CancellationToken.None);

        Assert.Equal("accepted", result.Status);
        Assert.Equal("0100019a-abc", result.MessageId);
        Assert.Equal(expectedPath, handler.LastRequest!.RequestUri!.AbsolutePath);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("42", body.RootElement.GetProperty("invoice").GetProperty("invoiceNumber").GetString());
        Assert.Equal("none", body.RootElement.GetProperty("options").GetProperty("footerBrand").GetString());
        var email = body.RootElement.GetProperty("email");
        Assert.Equal("invoices@acme.example", email.GetProperty("from").GetString());
        Assert.Equal("Acme Billing", email.GetProperty("fromName").GetString());
        Assert.Equal("ap@customer.example", email.GetProperty("to").GetString());
        Assert.Equal(2, email.GetProperty("cc").GetArrayLength());
        Assert.Equal("billing@acme.example", email.GetProperty("replyTo").GetString());
    }

    [Theory]
    [InlineData(PdfInvoiceFormat.FacturX, "/v1/embed/facturx/email")]
    [InlineData(PdfInvoiceFormat.Zugferd, "/v1/embed/zugferd/email")]
    public async Task EmailEmbeddedAsync_PostsTheFilesAndTheEmailFormFields(PdfInvoiceFormat format, string expectedPath)
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK, Accepted));
        var client = new HttpInvoiceXmlClient(BuildHttp(handler));

        var result = await client.EmailEmbeddedAsync(format, SamplePdf, SampleCii, Email, rules: null);

        Assert.Equal("0100019a-abc", result.MessageId);
        Assert.Equal(expectedPath, handler.LastRequest!.RequestUri!.AbsolutePath);

        var body = handler.LastRequestBody!;
        Assert.Contains("name=pdf", body);
        Assert.Contains("name=xml", body);
        Assert.Contains("name=email.from", body);
        Assert.Contains("name=email.fromName", body);
        Assert.Contains("name=email.to", body);
        Assert.Contains("name=email.bcc", body);
        Assert.Contains("name=email.replyTo", body);
        Assert.Contains("name=email.subject", body);
        Assert.Contains("name=email.message", body);
        // One form part per copy address, the way the API binds the array.
        Assert.Equal(2, body.Split("name=email.cc").Length - 1);
    }

    [Fact]
    public async Task EmailEmbeddedAsync_LeavesOutTheFieldsThatAreNotSet()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.OK, Accepted));
        var client = new HttpInvoiceXmlClient(BuildHttp(handler));

        await client.EmailEmbeddedAsync(
            PdfInvoiceFormat.Zugferd, SamplePdf, SampleCii,
            new EmailMessage { From = "invoices@acme.example", To = "ap@customer.example" },
            rules: null);

        var body = handler.LastRequestBody!;
        Assert.Contains("name=email.from", body);
        Assert.Contains("name=email.to", body);
        Assert.DoesNotContain("email.cc", body);
        Assert.DoesNotContain("email.subject", body);
        Assert.DoesNotContain("email.message", body);
    }

    [Fact]
    public async Task EmailInvoice_ReturnsTheMessageIdAndNoFile()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd, new InvoiceDocument { InvoiceNumber = "42" }, Email, null, CancellationToken.None);

        Assert.False(result.IsError ?? false);
        Assert.Equal(InvoiceFormat.Zugferd, client.LastEmailFormat);
        Assert.Same(Email, client.LastEmail);
        Assert.All(result.Content, block => Assert.IsType<TextContentBlock>(block));
        Assert.Contains("0100019a-abc", ((TextContentBlock)result.Content[0]).Text);
        Assert.Contains("ap@customer.example", ((TextContentBlock)result.Content[0]).Text);
    }

    [Fact]
    public async Task EmailInvoice_RefusesRulesTheFormatDoesNotTakeBeforeSending()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd,
            new InvoiceDocument { InvoiceNumber = "42" },
            Email,
            new CreateInvoiceOptions { Rules = [ExtraRuleset.BrFr] },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Null(client.LastEmail);
    }

    [Fact]
    public async Task EmailEmbedded_ForwardsTheResolvedFiles()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmailEmbeddedInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        var result = await tool.EmailEmbeddedInvoiceAsync(
            PdfInvoiceFormat.FacturX, Email, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf), xml: SampleCii);

        Assert.False(result.IsError ?? false);
        Assert.Equal(PdfInvoiceFormat.FacturX, client.LastEmbedFormat);
        Assert.Equal(SamplePdf, client.LastEmbedPdf);
        Assert.Equal(SampleCii, client.LastEmbedXml);
        Assert.Same(Email, client.LastEmail);
    }

    [Fact]
    public async Task EmailEmbedded_ForwardsTheRules()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmailEmbeddedInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        await tool.EmailEmbeddedInvoiceAsync(
            PdfInvoiceFormat.FacturX, Email, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf), xml: SampleCii, rules: [ExtraRuleset.BrFr]);

        Assert.Equal([ExtraRuleset.BrFr], client.LastEmailRules);
    }

    [Fact]
    public async Task EmailEmbedded_RefusesRulesTheFormatDoesNotTakeBeforeSending()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmailEmbeddedInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        var result = await tool.EmailEmbeddedInvoiceAsync(
            PdfInvoiceFormat.Zugferd, Email, CancellationToken.None,
            pdfBase64: Convert.ToBase64String(SamplePdf), xml: SampleCii, rules: [ExtraRuleset.BrFr]);

        Assert.True(result.IsError);
        Assert.Contains("INPUT-RULES", ((TextContentBlock)result.Content[0]).Text);
        Assert.Null(client.LastEmail);
    }

    [Fact]
    public async Task EmailInvoice_RefusesAnInvalidLogoBeforeSending()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd,
            new InvoiceDocument { InvoiceNumber = "42" },
            Email,
            new CreateInvoiceOptions { Logo = "not base64!" },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("INPUT-LOGO", ((TextContentBlock)result.Content[0]).Text);
        Assert.Null(client.LastEmail);
    }

    [Fact]
    public async Task EmailEmbedded_NeedsExactlyOnePdfSource()
    {
        var client = new CapturingInvoiceXmlClient();
        var tool = new EmailEmbeddedInvoiceTool(client, new FakeRemoteFileFetcher(SamplePdf));

        var result = await tool.EmailEmbeddedInvoiceAsync(
            PdfInvoiceFormat.FacturX, Email, CancellationToken.None, xml: SampleCii);

        Assert.True(result.IsError);
        Assert.Null(client.LastEmail);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task AServerFailure_TellsTheAgentNotToSendAgainOnItsOwn(HttpStatusCode status)
    {
        var client = new CapturingInvoiceXmlClient { EmailFailure = new InvoiceXmlApiException(status, "{}") };
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd, new InvoiceDocument { InvoiceNumber = "42" }, Email, null, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("may already have gone out", ((TextContentBlock)result.Content[0]).Text);
    }

    [Fact]
    public async Task ALostConnection_TellsTheAgentNotToSendAgainOnItsOwn()
    {
        var client = new CapturingInvoiceXmlClient { EmailFailure = new HttpRequestException("connection reset") };
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd, new InvoiceDocument { InvoiceNumber = "42" }, Email, null, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("may already have gone out", ((TextContentBlock)result.Content[0]).Text);
    }

    [Fact]
    public async Task ATimeout_SaysTheOutcomeIsUnknownInTheSummaryToo()
    {
        var client = new CapturingInvoiceXmlClient { EmailFailure = new TaskCanceledException("timed out") };
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd, new InvoiceDocument { InvoiceNumber = "42" }, Email, null, CancellationToken.None);

        Assert.True(result.IsError);
        var text = ((TextContentBlock)result.Content[0]).Text;
        Assert.Contains("outcome of this send is unknown", text);
        Assert.DoesNotContain("Could not reach", text);
    }

    [Fact]
    public async Task AnUnreadableAnswer_SaysTheEmailMayHaveGoneOut()
    {
        var client = new CapturingInvoiceXmlClient { EmailFailure = new JsonException("no body") };
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd, new InvoiceDocument { InvoiceNumber = "42" }, Email, null, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("may already have gone out", ((TextContentBlock)result.Content[0]).Text);
    }

    [Fact]
    public async Task ARefusedEmail_SaysNothingWasSent_AndOffersThePlanAndSupportRoutes()
    {
        var client = new CapturingInvoiceXmlClient
        {
            EmailFailure = new InvoiceXmlApiException(HttpStatusCode.Forbidden, "{\"detail\":\"Your plan does not include email delivery.\"}"),
        };
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd, new InvoiceDocument { InvoiceNumber = "42" }, Email, null, CancellationToken.None);

        Assert.True(result.IsError);
        var text = ((TextContentBlock)result.Content[0]).Text;
        Assert.Contains("Your plan does not include email delivery.", text);
        Assert.Contains("Nothing was sent", text);
        Assert.Contains("support@invoicexml.com", text);
        Assert.DoesNotContain("may already have gone out", text);
    }

    [Fact]
    public async Task ARejectedRequest_KeepsTheAgentOnThisTool_AndAwayFromChangingAddresses()
    {
        var client = new CapturingInvoiceXmlClient
        {
            EmailFailure = new InvoiceXmlApiException(HttpStatusCode.BadRequest, "{\"detail\":\"Domain not verified.\",\"errorCode\":4022}"),
        };
        var tool = new EmailInvoiceTool(client);

        var result = await tool.EmailInvoiceAsync(
            InvoiceFormat.Zugferd, new InvoiceDocument { InvoiceNumber = "42" }, Email, null, CancellationToken.None);

        Assert.True(result.IsError);
        var text = ((TextContentBlock)result.Content[0]).Text;
        Assert.Contains("Nothing was sent", text);
        Assert.Contains("Never change the sender", text);
        Assert.Contains("https://www.invoicexml.com/account/sending-domains", text);
        Assert.DoesNotContain("create_invoice", text);
    }

    [Fact]
    public void EveryEmailTool_ToldTheAgentWhereTheDomainIsSetUp_BeforeTheFirstFailure() =>
        Assert.Contains("https://www.invoicexml.com/account/sending-domains", EmailTools.OutcomeGuidance);

    [Fact]
    public void EmailInvoice_NamesTheFieldTheRulesGoIn()
    {
        var tool = McpServerTool.Create(typeof(EmailInvoiceTool).GetMethod(nameof(EmailInvoiceTool.EmailInvoiceAsync))!, typeof(EmailInvoiceTool));

        Assert.Contains("options.rules", tool.ProtocolTool.Description);
    }

    [Theory]
    [InlineData("reply-to address")]
    [InlineData("display name")]
    [InlineData("any copies")]
    public void TheConfirmationRuleCoversEveryAddressField(string field) =>
        Assert.Contains(field, EmailTools.ConfirmationGuidance);

    [Theory]
    [InlineData("email.from")]
    [InlineData("email.fromName")]
    [InlineData("email.to")]
    [InlineData("email.cc")]
    [InlineData("email.bcc")]
    [InlineData("email.replyTo")]
    [InlineData("email.subject")]
    [InlineData("email.message")]
    [InlineData("invoice.invoiceNumber")]
    [InlineData("options.footerBrand")]
    public void EmailInvoice_SchemaAdvertisesField(string path)
    {
        var schema = ToolSchemaTests.InputSchema<EmailInvoiceTool>(nameof(EmailInvoiceTool.EmailInvoiceAsync));

        Assert.True(ToolSchemaTests.TryResolve(schema, path, out _), $"email_invoice schema does not expose '{path}'.");
    }

    [Theory]
    [InlineData("email.from")]
    [InlineData("email.to")]
    [InlineData("pdfUrl")]
    [InlineData("pdfBase64")]
    [InlineData("xml")]
    [InlineData("xmlUrl")]
    [InlineData("rules")]
    [InlineData("validationLanguage")]
    public void EmailEmbedded_SchemaAdvertisesField(string path)
    {
        var schema = ToolSchemaTests.InputSchema<EmailEmbeddedInvoiceTool>(nameof(EmailEmbeddedInvoiceTool.EmailEmbeddedInvoiceAsync));

        Assert.True(ToolSchemaTests.TryResolve(schema, path, out _), $"email_embedded_invoice schema does not expose '{path}'.");
    }

    // Sending cannot be undone, so clients must treat these calls as confirm-first and the
    // agent must be told to get the user's go-ahead.
    [Theory]
    [InlineData(typeof(EmailInvoiceTool), nameof(EmailInvoiceTool.EmailInvoiceAsync))]
    [InlineData(typeof(EmailEmbeddedInvoiceTool), nameof(EmailEmbeddedInvoiceTool.EmailEmbeddedInvoiceAsync))]
    public void EmailTools_AreMarkedDestructive_AndTellTheAgentToConfirmFirst(Type toolType, string methodName)
    {
        var tool = McpServerTool.Create(toolType.GetMethod(methodName)!, toolType).ProtocolTool;

        Assert.True(tool.Annotations?.DestructiveHint);
        Assert.False(tool.Annotations?.IdempotentHint);
        Assert.Contains(EmailTools.ConfirmationGuidance, tool.Description);
        Assert.Contains(EmailTools.OutcomeGuidance, tool.Description);
    }

    [Theory]
    [InlineData(typeof(EmailInvoiceTool), nameof(EmailInvoiceTool.EmailInvoiceAsync))]
    [InlineData(typeof(EmailEmbeddedInvoiceTool), nameof(EmailEmbeddedInvoiceTool.EmailEmbeddedInvoiceAsync))]
    public void EmailTools_TellTheAgentToApplyBrFrByDefault(Type toolType, string methodName)
    {
        var description = McpServerTool.Create(toolType.GetMethod(methodName)!, toolType).ProtocolTool.Description;

        Assert.Contains(ExtraRules.FacturXDefaultGuidance, description);
    }
}
