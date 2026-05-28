using System.ComponentModel.DataAnnotations;

namespace InvoiceXml.Mcp.Core.Options;

/// <summary>
/// Configures how the SDK reaches the InvoiceXML public API.
/// Bound from the <c>InvoiceXml</c> configuration section.
/// </summary>
/// <remarks>
/// This type intentionally contains <strong>no authentication material</strong>.
/// Credentials are injected by the host as an <see cref="HttpClient"/> message handler
/// so the SDK stays independent of how the host authenticates (static API key,
/// OAuth-resolved per-request token, mTLS, etc.).
/// </remarks>
public sealed class InvoiceXmlClientOptions
{
    /// <summary>Configuration section name: <c>InvoiceXml</c>.</summary>
    public const string SectionName = "InvoiceXml";

    /// <summary>Base URL of the InvoiceXML API. Defaults to the public production endpoint.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.invoicexml.com";

    /// <summary>Per-request timeout. Defaults to 100 seconds, matching <see cref="HttpClient"/>'s default.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}
