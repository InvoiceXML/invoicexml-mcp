using InvoiceXml.Mcp.Core.Services;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Input arbitration shared by the tools that embed a CII XML into a PDF
/// (<c>embed_invoice</c>, <c>email_embedded_invoice</c>): exactly one source for each
/// file, the actual bytes behind it, and no truncated PDF.
/// </summary>
internal static class EmbedInputs
{
    public sealed record Resolved(byte[] Pdf, string CiiXml);

    /// <summary>Returns the two files, or a ready-to-return failure result.</summary>
    public static async Task<(Resolved? Inputs, CallToolResult? Error)> ResolveAsync(
        IRemoteFileFetcher fetcher,
        string? pdfUrl,
        string? pdfBase64,
        string? xml,
        string? xmlUrl,
        string slug,
        CancellationToken cancellationToken)
    {
        var pdfExclusive = ArtifactTools.ValidateExactlyOne(
        [
            ("pdfUrl", !string.IsNullOrWhiteSpace(pdfUrl)),
            ("pdfBase64", !string.IsNullOrWhiteSpace(pdfBase64)),
        ], slug);
        if (pdfExclusive is not null)
            return (null, pdfExclusive);

        var xmlExclusive = ArtifactTools.ValidateExactlyOne(
        [
            ("xml", !string.IsNullOrWhiteSpace(xml)),
            ("xmlUrl", !string.IsNullOrWhiteSpace(xmlUrl)),
        ], slug);
        if (xmlExclusive is not null)
            return (null, xmlExclusive);

        byte[] pdfBytes;
        if (!string.IsNullOrWhiteSpace(pdfUrl))
        {
            var (fetched, error) = await ArtifactTools
                .FetchUrlAsync(fetcher, pdfUrl, "pdfUrl", slug, cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
                return (null, error);
            pdfBytes = fetched!;
        }
        else
        {
            try
            {
                pdfBytes = Convert.FromBase64String(pdfBase64!);
            }
            catch (FormatException)
            {
                return (null, ArtifactTools.InputError("INPUT-BASE64",
                    "pdfBase64 is not valid base64. Use standard base64 (no chunking, no URL-safe alphabet). " +
                    "For anything but a small file, pass a public https:// URL via pdfUrl instead.",
                    ["pdfBase64"], slug));
            }
        }

        if (PdfSniffer.IsIncompletePdf(pdfBytes))
        {
            return (null, ArtifactTools.InputError("INPUT-INCOMPLETE-PDF",
                $"Received {pdfBytes.Length:N0} bytes that start like a PDF but have no %%EOF trailer; " +
                "the file is truncated or was reconstructed. If you don't have the real file bytes, " +
                "do not rebuild them: pass a public https:// URL via pdfUrl instead.",
                ["pdfBase64", "pdfUrl"], slug));
        }

        string ciiXml;
        if (!string.IsNullOrWhiteSpace(xml))
        {
            ciiXml = xml;
        }
        else
        {
            var (fetched, error) = await ArtifactTools
                .FetchUrlAsync(fetcher, xmlUrl!, "xmlUrl", slug, cancellationToken)
                .ConfigureAwait(false);
            if (error is not null)
                return (null, error);
            ciiXml = ArtifactTools.DecodeUtf8(fetched!);
        }

        return (new Resolved(pdfBytes, ciiXml), null);
    }
}
