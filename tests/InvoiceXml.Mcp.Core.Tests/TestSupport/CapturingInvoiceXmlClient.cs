using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;

namespace InvoiceXml.Mcp.Core.Tests.TestSupport;

/// <summary>
/// <see cref="IInvoiceXmlClient"/> test double that records what each method was
/// called with and returns configurable canned results: a <see cref="ValidationResult"/>
/// (valid by default) for the validate methods, and a <see cref="DocumentArtifact"/>
/// for the artifact-producing methods (render / extract / embed / convert / create).
/// </summary>
internal sealed class CapturingInvoiceXmlClient : IInvoiceXmlClient
{
    private readonly ValidationResult _result;
    private readonly DocumentArtifact _artifact;

    public CapturingInvoiceXmlClient(ValidationResult? result = null, DocumentArtifact? artifact = null)
    {
        _result = result ?? new ValidationResult { Valid = true };
        _artifact = artifact ?? new DocumentArtifact
        {
            Content = [0x25, 0x50, 0x44, 0x46], // "%PDF"
            ContentType = "application/pdf",
            FileName = "out.pdf",
        };
    }

    public PdfInvoiceFormat? LastPdfFormat { get; private set; }
    public byte[]? LastPdfBytes { get; private set; }

    public XmlInvoiceFormat? LastXmlFormat { get; private set; }
    public string? LastXml { get; private set; }

    public XmlInvoiceFormat? LastRenderFormat { get; private set; }
    public string? LastRenderXml { get; private set; }
    public PdfLanguage? LastRenderLanguage { get; private set; }

    public ExtractTarget? LastExtractTarget { get; private set; }
    public byte[]? LastExtractContent { get; private set; }
    public string? LastExtractContentType { get; private set; }
    public string? LastExtractFileName { get; private set; }

    public PdfInvoiceFormat? LastEmbedFormat { get; private set; }
    public byte[]? LastEmbedPdf { get; private set; }
    public string? LastEmbedXml { get; private set; }

    public InvoiceFormat? LastConvertSource { get; private set; }
    public InvoiceFormat? LastConvertTarget { get; private set; }
    public byte[]? LastConvertContent { get; private set; }
    public string? LastConvertContentType { get; private set; }

    public InvoiceFormat? LastTransformTarget { get; private set; }
    public byte[]? LastTransformPdf { get; private set; }
    public PdfLanguage? LastTransformLanguage { get; private set; }
    public string? LastTransformBuyerReference { get; private set; }

    public ValidationReportFormat? LastReportFormat { get; private set; }
    public byte[]? LastReportContent { get; private set; }
    public string? LastReportContentType { get; private set; }
    public string? LastReportFileName { get; private set; }
    public bool? ReportVerdict { get; set; } = true;

    // Optional fields forwarded by whichever method ran last.
    public IReadOnlyList<ExtraRuleset>? LastRules { get; private set; }
    public FooterBrand? LastFooterBrand { get; private set; }
    public string? LastLogoUrl { get; private set; }

    public OrderFormat? LastOrderFormat { get; private set; }
    public InvoiceDocument? LastOrder { get; private set; }
    public CreateOrderOptions? LastOrderOptions { get; private set; }

    public byte[]? LastOrderValidationContent { get; private set; }
    public string? LastOrderValidationContentType { get; private set; }

    public AccountInfo Account { get; set; } = new()
    {
        Plan = "subscription",
        CreditsRemaining = 1234,
        CreditsTotal = 5000,
        CreditsConsumed = 3766,
        CreditsConsumedTotal = 20000,
    };

    public InvoiceFormat? LastCreateFormat { get; private set; }
    public CreateInvoiceOptions? LastCreateOptions { get; private set; }

    public Task<CreateInvoiceResult> CreateInvoiceAsync(
        InvoiceFormat format, InvoiceDocument invoice, CreateInvoiceOptions? options, CancellationToken cancellationToken = default)
    {
        LastCreateFormat = format;
        LastCreateOptions = options;
        return Task.FromResult(new CreateInvoiceResult
        {
            Content = _artifact.Content,
            ContentType = _artifact.ContentType,
            FileName = _artifact.FileName,
        });
    }

    public Task<DocumentArtifact> CreateOrderAsync(
        OrderFormat format, InvoiceDocument order, CreateOrderOptions? options, CancellationToken cancellationToken = default)
    {
        LastOrderFormat = format;
        LastOrder = order;
        LastOrderOptions = options;
        return Task.FromResult(_artifact);
    }

    public Task<ValidationResult> ValidateOrderAsync(
        byte[] content, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        LastOrderValidationContent = content;
        LastOrderValidationContentType = contentType;
        return Task.FromResult(_result);
    }

    public Task<AccountInfo> GetAccountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Account);

    public Task<ValidationResult> ValidateXmlAsync(
        XmlInvoiceFormat format, string xml, IReadOnlyList<ExtraRuleset>? rules, CancellationToken cancellationToken = default)
    {
        LastRules = rules;
        LastXmlFormat = format;
        LastXml = xml;
        return Task.FromResult(_result);
    }

    public Task<ValidationResult> ValidatePdfAsync(
        PdfInvoiceFormat format, byte[] pdf, IReadOnlyList<ExtraRuleset>? rules, CancellationToken cancellationToken = default)
    {
        LastRules = rules;
        LastPdfFormat = format;
        LastPdfBytes = pdf;
        return Task.FromResult(_result);
    }

    public Task<ValidationReportPdfResult> ValidationReportPdfAsync(
        ValidationReportFormat format, byte[] content, string contentType, string fileName, IReadOnlyList<ExtraRuleset>? rules, FooterBrand? footerBrand, CancellationToken cancellationToken = default)
    {
        LastRules = rules;
        LastFooterBrand = footerBrand;
        LastReportFormat = format;
        LastReportContent = content;
        LastReportContentType = contentType;
        LastReportFileName = fileName;
        return Task.FromResult(new ValidationReportPdfResult { Report = _artifact, Valid = ReportVerdict });
    }

    public Task<DocumentArtifact> RenderToPdfAsync(
        XmlInvoiceFormat format, string xml, PdfLanguage language, string? logoUrl, FooterBrand? footerBrand, CancellationToken cancellationToken = default)
    {
        LastLogoUrl = logoUrl;
        LastFooterBrand = footerBrand;
        LastRenderFormat = format;
        LastRenderXml = xml;
        LastRenderLanguage = language;
        return Task.FromResult(_artifact);
    }

    public Task<DocumentArtifact> ExtractAsync(
        ExtractTarget target, byte[] content, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        LastExtractTarget = target;
        LastExtractContent = content;
        LastExtractContentType = contentType;
        LastExtractFileName = fileName;
        return Task.FromResult(_artifact);
    }

    public Task<DocumentArtifact> EmbedAsync(
        PdfInvoiceFormat format, byte[] pdf, string ciiXml, IReadOnlyList<ExtraRuleset>? rules, CancellationToken cancellationToken = default)
    {
        LastRules = rules;
        LastEmbedFormat = format;
        LastEmbedPdf = pdf;
        LastEmbedXml = ciiXml;
        return Task.FromResult(_artifact);
    }

    public Task<DocumentArtifact> ConvertAsync(
        InvoiceFormat source, InvoiceFormat target, byte[] content, string contentType, string fileName, FooterBrand? footerBrand, CancellationToken cancellationToken = default)
    {
        LastFooterBrand = footerBrand;
        LastConvertSource = source;
        LastConvertTarget = target;
        LastConvertContent = content;
        LastConvertContentType = contentType;
        return Task.FromResult(_artifact);
    }

    public Task<DocumentArtifact> TransformAsync(
        InvoiceFormat target, byte[] pdf, PdfLanguage language, string? buyerReference, CancellationToken cancellationToken = default)
    {
        LastTransformTarget = target;
        LastTransformPdf = pdf;
        LastTransformLanguage = language;
        LastTransformBuyerReference = buyerReference;
        return Task.FromResult(_artifact);
    }
}
