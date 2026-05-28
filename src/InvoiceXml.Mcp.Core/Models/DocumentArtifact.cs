namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// A file returned by a transform endpoint (<c>/render</c>, <c>/extract</c>,
/// <c>/embed</c>, <c>/convert</c>). The body is binary because the output may be
/// a PDF (hybrid formats, rendered previews) or UTF-8 text (XML / JSON) depending
/// on the operation; the tool layer decides how to present it to the LLM based on
/// <see cref="ContentType"/> (text inline, binary as a downloadable attachment).
/// </summary>
public sealed class DocumentArtifact
{
    /// <summary>The raw response body. UTF-8 text for XML/JSON outputs, PDF bytes for hybrid/preview outputs.</summary>
    public required byte[] Content { get; init; }

    /// <summary>HTTP <c>Content-Type</c> of the response (e.g. <c>application/xml</c>, <c>application/json</c>, <c>application/pdf</c>).</summary>
    public required string ContentType { get; init; }

    /// <summary>Suggested file name parsed from <c>Content-Disposition</c>, or a sensible default.</summary>
    public required string FileName { get; init; }
}
