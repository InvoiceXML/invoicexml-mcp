using System.Text;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Protocol;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

public class ToolResultsTests
{
    [Fact]
    public void ForArtifact_WithXml_EmitsTwoTextBlocks()
    {
        const string xml = "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\"/>";
        var bytes = Encoding.UTF8.GetBytes(xml);

        var result = ToolResults.ForArtifact(
            summary: "Created UBL XML (97 bytes) as invoice-1-ubl.xml.",
            content: bytes,
            contentType: "application/xml",
            fileName: "invoice-1-ubl.xml");

        Assert.False(result.IsError ?? false);
        Assert.Equal(2, result.Content.Count);

        var summary = Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.StartsWith("Created UBL XML", summary.Text);

        var body = Assert.IsType<TextContentBlock>(result.Content[1]);
        Assert.Equal(xml, body.Text);
    }

    [Fact]
    public void ForArtifact_WithJson_StillInlinedAsText()
    {
        // application/json is textual; the LLM might want to reason about it,
        // so it goes inline rather than as a downloadable attachment.
        var json = "{\"foo\":42}"u8.ToArray();

        var result = ToolResults.ForArtifact("Summary", json, "application/json", "data.json");

        Assert.IsType<TextContentBlock>(result.Content[1]);
    }

    [Fact]
    public void ForArtifact_WithPdf_EmitsEmbeddedResourceBlock()
    {
        // Real PDF magic bytes so the test reads like a real artefact.
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };

        var result = ToolResults.ForArtifact(
            summary: "Created facturx hybrid PDF (8 bytes) as invoice-factur-x.pdf.",
            content: pdf,
            contentType: "application/pdf",
            fileName: "invoice-factur-x.pdf");

        Assert.False(result.IsError ?? false);
        Assert.Equal(2, result.Content.Count);
        Assert.IsType<TextContentBlock>(result.Content[0]);

        var embedded = Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
        var blob = Assert.IsType<BlobResourceContents>(embedded.Resource);

        Assert.Equal("application/pdf", blob.MimeType);
        Assert.Equal("attachment://invoice-factur-x.pdf", blob.Uri);
        // DecodedData round-trips back to the original raw bytes.
        Assert.Equal(pdf, blob.DecodedData.ToArray());
    }

    [Fact]
    public void ForArtifact_WithOctetStream_TreatedAsBinary()
    {
        // Generic binary content type — the helper must still pick the
        // resource path so the bytes don't get UTF-8 decoded and mangled.
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0xFF };

        var result = ToolResults.ForArtifact("summary", bytes, "application/octet-stream", "blob.bin");

        var embedded = Assert.IsType<EmbeddedResourceBlock>(result.Content[1]);
        var blob = Assert.IsType<BlobResourceContents>(embedded.Resource);
        Assert.Equal("application/octet-stream", blob.MimeType);
        Assert.Equal(bytes, blob.DecodedData.ToArray());
    }

    [Fact]
    public void ForArtifact_RejectsEmptySummary()
    {
        Assert.Throws<ArgumentException>(() =>
            ToolResults.ForArtifact(string.Empty, [1, 2, 3], "application/pdf", "f.pdf"));
    }

    [Fact]
    public void ForFailure_SetsIsErrorAndJsonSerializesPayload()
    {
        var payload = new
        {
            success = false,
            summary = "Validation failed.",
            failureCategory = "Validation",
            errors = new[] { new { message = "Buyer postal address (BG-8) is required." } },
        };

        var result = ToolResults.ForFailure(payload);

        Assert.True(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // camelCase + ignore-null per InvoiceXmlJsonOptions; we don't need to
        // deserialise back, just assert the right text appears.
        Assert.Contains("\"success\":false", block.Text);
        Assert.Contains("\"failureCategory\":\"Validation\"", block.Text);
        Assert.Contains("Buyer postal address", block.Text);
    }
}
