using InvoiceXml.Mcp.Core.Services;

namespace InvoiceXml.Mcp.Core.Tests.TestSupport;

/// <summary>Returns canned bytes, captures the requested URL, or throws a configured <see cref="FileFetchException"/>.</summary>
internal sealed class FakeRemoteFileFetcher : IRemoteFileFetcher
{
    private readonly byte[]? _bytes;
    private readonly FileFetchException? _throw;

    public FakeRemoteFileFetcher(byte[] bytes) => _bytes = bytes;
    public FakeRemoteFileFetcher(FileFetchException toThrow) => _throw = toThrow;

    public string? LastUrl { get; private set; }

    public Task<byte[]> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        LastUrl = url;
        if (_throw is not null)
            throw _throw;
        return Task.FromResult(_bytes!);
    }
}
