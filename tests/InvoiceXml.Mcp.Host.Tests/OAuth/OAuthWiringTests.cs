using InvoiceXml.Mcp.Core.Extensions;
using InvoiceXml.Mcp.Host.Auth;
using InvoiceXml.Mcp.Host.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Host.Tests.OAuth;

public class OAuthWiringTests
{
    [Fact]
    public void OAuthMode_BuildsServiceProviderAndValidatesOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpUri"] = "https://mcp.invoicexml.test",
                ["InvoiceXml:BaseUrl"] = "https://api.invoicexml.test",
                ["Mcp:OAuth:AuthorizationServer"] = "https://invoicexml.test",
                ["Mcp:OAuth:ScopesSupported:0"] = "api_token.read",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInvoiceXmlMcpCore(configuration)
            .AddHostAuth(configuration, AuthMode.OAuth);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        // Validate-on-start kicks the validators; reach them by resolving the options.
        var oauth = provider.GetRequiredService<IOptions<Host.Auth.OAuth.OAuthOptions>>().Value;
        Assert.Equal("https://invoicexml.test", oauth.AuthorizationServer);
        Assert.Single(oauth.ScopesSupported, "api_token.read");

        var deployment = provider.GetRequiredService<IOptions<McpDeploymentOptions>>().Value;
        Assert.Equal("https://mcp.invoicexml.test", deployment.McpUri);
    }

    [Fact]
    public void OAuthMode_RejectsMissingMcpUri()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InvoiceXml:BaseUrl"] = "https://api.invoicexml.test",
                ["Mcp:OAuth:AuthorizationServer"] = "https://invoicexml.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInvoiceXmlMcpCore(configuration)
            .AddHostAuth(configuration, AuthMode.OAuth);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<McpDeploymentOptions>>().Value);
    }

    [Fact]
    public void OAuthMode_RejectsMissingAuthorizationServer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpUri"] = "https://mcp.invoicexml.test",
                ["InvoiceXml:BaseUrl"] = "https://api.invoicexml.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInvoiceXmlMcpCore(configuration)
            .AddHostAuth(configuration, AuthMode.OAuth);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<Host.Auth.OAuth.OAuthOptions>>().Value);
    }
}
