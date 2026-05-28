namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Outcome of a <c>/create</c> call. The body is binary because some formats
/// (Factur-X / ZUGFeRD) return a PDF; XML formats return UTF-8 XML in the same
/// byte buffer. The tool layer decides how to present the bytes to the LLM
/// based on <see cref="ContentType"/>.
/// </summary>
public sealed class CreateInvoiceResult
{
    /// <summary>The raw response body. UTF-8 XML for XML formats, PDF bytes for hybrid formats.</summary>
    public required byte[] Content { get; init; }

    /// <summary>HTTP <c>Content-Type</c> of the response (e.g. <c>application/xml</c>, <c>application/pdf</c>).</summary>
    public required string ContentType { get; init; }

    /// <summary>Suggested file name parsed from <c>Content-Disposition</c>, or a sensible default.</summary>
    public required string FileName { get; init; }
}
