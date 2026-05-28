namespace InvoiceXml.Mcp.Host.Configuration;

/// <summary>
/// Top-level host settings bound from the <c>Mcp</c> configuration section.
/// Mode-specific settings live in their own option types alongside their auth wiring
/// (e.g. <see cref="Auth.ApiKey.ApiKeyOptions"/>), to keep this surface focused.
/// </summary>
public sealed class McpHostOptions
{
    /// <summary>Configuration section name: <c>Mcp</c>.</summary>
    public const string SectionName = "Mcp";

    /// <summary>Which authentication mode the host runs in. Defaults to <see cref="AuthMode.ApiKey"/>.</summary>
    public AuthMode AuthMode { get; set; } = AuthMode.ApiKey;
}
