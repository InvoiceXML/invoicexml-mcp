namespace InvoiceXml.Mcp.Core.Services;

/// <summary>
/// Downloads a file from a caller-supplied URL, applying SSRF protections
/// (HTTPS only, public IPs only) and a size cap. Used by the validation tools
/// to support a URL input mode without making the LLM carry the bytes.
/// </summary>
public interface IRemoteFileFetcher
{
    /// <summary>
    /// Fetches the content at <paramref name="url"/>. Throws
    /// <see cref="FileFetchException"/> for any policy or transport failure.
    /// </summary>
    Task<byte[]> FetchAsync(string url, CancellationToken cancellationToken = default);
}
