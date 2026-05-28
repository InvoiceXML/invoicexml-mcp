namespace InvoiceXml.Mcp.Host.Configuration;

/// <summary>
/// Selects how the host authenticates outbound calls to the InvoiceXML API.
/// </summary>
/// <remarks>
/// The value is read from <c>Mcp:AuthMode</c> at startup. New modes are added by
/// extending this enum and registering the matching wiring in
/// <see cref="Auth.AuthExtensions.AddHostAuth"/>.
/// </remarks>
public enum AuthMode
{
    /// <summary>
    /// Single static API key supplied via configuration or environment.
    /// Intended for self-hosted deployments where one user owns the key.
    /// </summary>
    ApiKey = 0,

    /// <summary>
    /// OAuth 2.1 with Dynamic Client Registration delegated to the InvoiceXML authorization server.
    /// Intended for the multi-tenant SaaS deployment. Implementation lands separately.
    /// </summary>
    OAuth = 1,
}
