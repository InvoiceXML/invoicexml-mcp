using InvoiceXml.Mcp.Core.Extensions;
using InvoiceXml.Mcp.Core.Interfaces;
using InvoiceXml.Mcp.Core.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInvoiceXmlMcpCore_RegistersTypedClientAndBindsOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InvoiceXml:BaseUrl"] = "https://api.example.com",
                ["InvoiceXml:Timeout"] = "00:00:30",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInvoiceXmlMcpCore(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<InvoiceXmlClientOptions>>().Value;
        Assert.Equal("https://api.example.com", options.BaseUrl);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);

        var client = provider.GetRequiredService<IInvoiceXmlClient>();
        Assert.NotNull(client);
    }
}
