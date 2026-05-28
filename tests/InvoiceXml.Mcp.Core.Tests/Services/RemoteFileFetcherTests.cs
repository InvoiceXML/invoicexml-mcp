using System.Net;
using InvoiceXml.Mcp.Core.Options;
using InvoiceXml.Mcp.Core.Services;
using InvoiceXml.Mcp.Core.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceXml.Mcp.Core.Tests.Services;

public class RemoteFileFetcherTests
{
    // ---- IsBlocked (pure SSRF policy) ---------------------------------------

    [Theory]
    [InlineData("127.0.0.1")]      // loopback
    [InlineData("::1")]            // loopback v6
    [InlineData("10.0.0.5")]       // private
    [InlineData("172.16.3.4")]     // private
    [InlineData("172.31.255.1")]   // private upper bound
    [InlineData("192.168.1.1")]    // private
    [InlineData("169.254.169.254")]// link-local (cloud metadata)
    [InlineData("100.64.0.1")]     // CGNAT
    [InlineData("0.0.0.0")]        // this-network
    [InlineData("224.0.0.1")]      // multicast
    [InlineData("fc00::1")]        // unique local v6
    [InlineData("fe80::1")]        // link-local v6
    public void IsBlocked_NonPublicAddresses_AreBlocked(string ip)
    {
        Assert.True(RemoteFileFetcher.IsBlocked(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("93.184.216.34")]  // public (example.com historically)
    [InlineData("1.1.1.1")]        // public
    [InlineData("8.8.8.8")]        // public
    [InlineData("2606:4700:4700::1111")] // public v6 (Cloudflare)
    public void IsBlocked_PublicAddresses_AreAllowed(string ip)
    {
        Assert.False(RemoteFileFetcher.IsBlocked(IPAddress.Parse(ip)));
    }

    // ---- FetchAsync (end-to-end with a stub transport) ----------------------

    private static IRemoteFileFetcher BuildFetcher(StubHttpMessageHandler handler, long maxBytes = 5 * 1024 * 1024)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new StaticOptionsMonitor<FileInputOptions>(new FileInputOptions
        {
            MaxFileSizeBytes = maxBytes,
            FetchTimeout = TimeSpan.FromSeconds(30),
        }));
        services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<FileInputOptions>>(
            sp => sp.GetRequiredService<StaticOptionsMonitor<FileInputOptions>>());
        services.AddHttpClient(RemoteFileFetcher.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddSingleton<IRemoteFileFetcher, RemoteFileFetcher>();

        return services.BuildServiceProvider().GetRequiredService<IRemoteFileFetcher>();
    }

    [Fact]
    public async Task FetchAsync_NonHttpsScheme_Rejected()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(HttpStatusCode.OK, [1], "application/pdf"));
        var fetcher = BuildFetcher(handler);

        var ex = await Assert.ThrowsAsync<FileFetchException>(() =>
            fetcher.FetchAsync("http://93.184.216.34/x.pdf", CancellationToken.None));

        Assert.Equal(FileFetchError.InvalidScheme, ex.Error);
        Assert.Null(handler.LastRequest); // never attempted
    }

    [Fact]
    public async Task FetchAsync_LoopbackHost_Blocked()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(HttpStatusCode.OK, [1], "application/pdf"));
        var fetcher = BuildFetcher(handler);

        var ex = await Assert.ThrowsAsync<FileFetchException>(() =>
            fetcher.FetchAsync("https://127.0.0.1/x.pdf", CancellationToken.None));

        Assert.Equal(FileFetchError.BlockedHost, ex.Error);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task FetchAsync_PrivateHost_Blocked()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(HttpStatusCode.OK, [1], "application/pdf"));
        var fetcher = BuildFetcher(handler);

        var ex = await Assert.ThrowsAsync<FileFetchException>(() =>
            fetcher.FetchAsync("https://169.254.169.254/latest/meta-data", CancellationToken.None));

        Assert.Equal(FileFetchError.BlockedHost, ex.Error);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task FetchAsync_PublicHost_ReturnsBytes()
    {
        var payload = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(HttpStatusCode.OK, payload, "application/pdf"));
        var fetcher = BuildFetcher(handler);

        var bytes = await fetcher.FetchAsync("https://93.184.216.34/invoice.pdf", CancellationToken.None);

        Assert.Equal(payload, bytes);
        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task FetchAsync_OverSizeCap_ThrowsTooLarge()
    {
        var payload = new byte[64];
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Binary(HttpStatusCode.OK, payload, "application/pdf"));
        var fetcher = BuildFetcher(handler, maxBytes: 16);

        var ex = await Assert.ThrowsAsync<FileFetchException>(() =>
            fetcher.FetchAsync("https://93.184.216.34/big.pdf", CancellationToken.None));

        Assert.Equal(FileFetchError.TooLarge, ex.Error);
    }

    [Fact]
    public async Task FetchAsync_NonSuccessStatus_ThrowsHttpError()
    {
        var handler = new StubHttpMessageHandler(StubHttpMessageHandler.Json(HttpStatusCode.NotFound, "{}"));
        var fetcher = BuildFetcher(handler);

        var ex = await Assert.ThrowsAsync<FileFetchException>(() =>
            fetcher.FetchAsync("https://93.184.216.34/missing.pdf", CancellationToken.None));

        Assert.Equal(FileFetchError.HttpError, ex.Error);
    }
}
