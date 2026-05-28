using System.Text;
using InvoiceXml.Mcp.Core.Tools;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class PdfSnifferTests
{
    [Fact]
    public void IsIncompletePdf_HeaderWithoutEof_IsTrue()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <<>> endobj");
        Assert.True(PdfSniffer.IsIncompletePdf(bytes));
    }

    [Fact]
    public void IsIncompletePdf_HeaderWithEof_IsFalse()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n... body ...\n%%EOF");
        Assert.False(PdfSniffer.IsIncompletePdf(bytes));
    }

    [Fact]
    public void IsIncompletePdf_NonPdfContent_IsFalse()
    {
        // XML and arbitrary bytes must be passed through untouched (no %PDF header).
        Assert.False(PdfSniffer.IsIncompletePdf(Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><Invoice/>")));
        Assert.False(PdfSniffer.IsIncompletePdf([0x00, 0x01, 0x02, 0x03]));
        Assert.False(PdfSniffer.IsIncompletePdf([]));
    }

    [Fact]
    public void IsIncompletePdf_EofFoundAnywhere_CountsAsComplete()
    {
        // Incremental-update / linearized PDFs can have trailing bytes after %%EOF.
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF\n<trailing incremental update bytes>");
        Assert.False(PdfSniffer.IsIncompletePdf(bytes));
    }
}
