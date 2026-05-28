using System.ComponentModel.DataAnnotations;

namespace InvoiceXml.Mcp.Host.Auth.ApiKey;

/// <summary>
/// Settings for the static API key authentication mode.
/// Bound from the <c>Mcp:ApiKey</c> configuration section.
/// </summary>
/// <remarks>
/// The key is a secret. <strong>Never</strong> commit a populated value to source control.
/// Provide it via one of:
/// <list type="bullet">
///   <item><description>environment variable <c>Mcp__ApiKey__Value</c> (preferred for deploys)</description></item>
///   <item><description>environment variable <c>INVOICEXML_API_KEY</c> (alias, friendlier spelling)</description></item>
///   <item><description><c>dotnet user-secrets</c> (preferred for local development)</description></item>
/// </list>
/// </remarks>
public sealed class ApiKeyOptions
{
    /// <summary>Configuration section name: <c>Mcp:ApiKey</c>.</summary>
    public const string SectionName = "Mcp:ApiKey";

    /// <summary>The InvoiceXML API key (Bearer token) used for every outbound request.</summary>
    [Required(AllowEmptyStrings = false)]
    public string Value { get; set; } = string.Empty;
}
