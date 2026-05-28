namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Byte-level structural sniff of a candidate PDF. Operates on raw bytes (never
/// decodes binary to a string) and only looks for the two mandatory ASCII markers
/// a complete PDF must have: the <c>%PDF</c> header at the start and a <c>%%EOF</c>
/// trailer somewhere in the file.
/// </summary>
/// <remarks>
/// This is a cheap safety net, not a parser. Its only job is to catch content that
/// announces itself as a PDF (<c>%PDF</c> header) but is truncated or fabricated
/// (no <c>%%EOF</c>) before such bytes are sent to the API. It deliberately stays
/// silent on anything that doesn't start with <c>%PDF</c> so it never second-guesses
/// non-PDF payloads (e.g. large XML uploaded through the same chunk mechanism).
/// </remarks>
internal static class PdfSniffer
{
    private static ReadOnlySpan<byte> PdfHeader => "%PDF"u8;
    private static ReadOnlySpan<byte> EofMarker => "%%EOF"u8;

    /// <summary>True when the bytes begin with the <c>%PDF</c> header.</summary>
    public static bool StartsWithPdfHeader(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(PdfHeader);

    /// <summary>True when the bytes contain a <c>%%EOF</c> marker anywhere.</summary>
    public static bool ContainsEofMarker(ReadOnlySpan<byte> bytes) =>
        bytes.IndexOf(EofMarker) >= 0;

    /// <summary>
    /// True only when the bytes look like a PDF (<c>%PDF</c> header) yet lack the
    /// mandatory <c>%%EOF</c> trailer — i.e. a truncated or reconstructed PDF.
    /// Returns <see langword="false"/> for complete PDFs and for any non-PDF content.
    /// </summary>
    public static bool IsIncompletePdf(ReadOnlySpan<byte> bytes) =>
        StartsWithPdfHeader(bytes) && !ContainsEofMarker(bytes);
}
