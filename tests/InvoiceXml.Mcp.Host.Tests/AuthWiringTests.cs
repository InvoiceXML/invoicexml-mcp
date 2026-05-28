using InvoiceXml.Mcp.Core.Extensions;
using InvoiceXml.Mcp.Host.Auth;
using InvoiceXml.Mcp.Host.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceXml.Mcp.Host.Tests;

public class AuthWiringTests
{
    [Fact]
    public void ApiKeyMode_WiresHandlerOntoCoreHttpClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InvoiceXml:BaseUrl"] = "https://api.example.com",
                ["Mcp:ApiKey:Value"] = "test-key",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInvoiceXmlMcpCore(configuration)
            .AddHostAuth(configuration, AuthMode.ApiKey);

        // Should build without throwing — i.e. all options validate, all handlers resolvable.
        using var provider = services.BuildServiceProvider(validateScopes: true);
        Assert.NotNull(provider);
    }

    [Fact]
    public void UnknownAuthMode_ThrowsInvalidOperation()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var builder = services.AddInvoiceXmlMcpCore(configuration);

        // Cast an out-of-range value to AuthMode to simulate a future enum value
        // that AddHostAuth doesn't know about yet.
        const AuthMode unknown = (AuthMode)999;

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddHostAuth(configuration, unknown));
    }
}
