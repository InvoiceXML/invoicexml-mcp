using InvoiceXml.Mcp.Core.Enums;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Models;

namespace InvoiceXml.Mcp.Core.Tests.TestSupport;

/// <summary>
/// <see cref="IInvoiceXmlClient"/> test double that records what each method was
/// called with and returns configurable canned results: a <see cref="ValidationResult"/>
/// (valid by default) for the validate methods, and a <see cref="DocumentArtifact"/>
/// for the artifact-producing methods (render / extract / embed / convert).
/// <c>CreateInvoiceAsync</c> is not used by these tests.
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
    public byte[]? LastExtractPdf { get; private set; }

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

    public Task<CreateInvoiceResult> CreateInvoiceAsync(
        InvoiceFormat format, InvoiceDocument invoice, PdfRenderOptions? options, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ValidationResult> ValidateXmlAsync(
        XmlInvoiceFormat format, string xml, CancellationToken cancellationToken = default)
    {
        LastXmlFormat = format;
        LastXml = xml;
        return Task.FromResult(_result);
    }

    public Task<ValidationResult> ValidatePdfAsync(
        PdfInvoiceFormat format, byte[] pdf, CancellationToken cancellationToken = default)
    {
        LastPdfFormat = format;
        LastPdfBytes = pdf;
        return Task.FromResult(_result);
    }

    public Task<DocumentArtifact> RenderToPdfAsync(
        XmlInvoiceFormat format, string xml, PdfLanguage language, CancellationToken cancellationToken = default)
    {
        LastRenderFormat = format;
        LastRenderXml = xml;
        LastRenderLanguage = language;
        return Task.FromResult(_artifact);
    }

    public Task<DocumentArtifact> ExtractAsync(
        ExtractTarget target, byte[] pdf, CancellationToken cancellationToken = default)
    {
        LastExtractTarget = target;
        LastExtractPdf = pdf;
        return Task.FromResult(_artifact);
    }

    public Task<DocumentArtifact> EmbedAsync(
        PdfInvoiceFormat format, byte[] pdf, string ciiXml, CancellationToken cancellationToken = default)
    {
        LastEmbedFormat = format;
        LastEmbedPdf = pdf;
        LastEmbedXml = ciiXml;
        return Task.FromResult(_artifact);
    }

    public Task<DocumentArtifact> ConvertAsync(
        InvoiceFormat source, InvoiceFormat target, byte[] content, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
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
