namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// Outcome of a <c>POST /v1/validate/{format}/report</c> call: the printable
/// PDF compliance report plus the validation verdict the API carries in the
/// <c>X-Invoice-Valid</c> response header, so callers can state the verdict
/// without parsing the PDF.
/// </summary>
public sealed class ValidationReportPdfResult
{
    /// <summary>The PDF compliance report as returned by the API.</summary>
    public required DocumentArtifact Report { get; init; }

    /// <summary>
    /// Verdict parsed from the <c>X-Invoice-Valid</c> response header:
    /// <see langword="true"/> when the invoice passed every layer,
    /// <see langword="false"/> when it did not, <see langword="null"/> when
    /// the header was absent or unreadable.
    /// </summary>
    public bool? Valid { get; init; }
}
