using System.Net;
using System.Net.Sockets;
using InvoiceXml.Mcp.Core.Options;
using Microsoft.Extensions.Options;

namespace InvoiceXml.Mcp.Core.Services;

/// <summary>
/// Default <see cref="IRemoteFileFetcher"/>. Resolves the target host, rejects
/// any address that isn't publicly routable (SSRF guard), follows a bounded
/// number of redirects re-validating each hop, and caps the response body at
/// <see cref="FileInputOptions.MaxFileSizeBytes"/>.
/// </summary>
internal sealed class RemoteFileFetcher : IRemoteFileFetcher
{
    /// <summary>Name of the dedicated, auth-free <see cref="HttpClient"/> this fetcher uses.</summary>
    public const string HttpClientName = "FileFetcher";

    private const int MaxRedirects = 3;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<FileInputOptions> _options;

    public RemoteFileFetcher(IHttpClientFactory httpClientFactory, IOptionsMonitor<FileInputOptions> options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<byte[]> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;

        // We control the total timeout ourselves so it also covers the body read
        // (HttpClient.Timeout doesn't, under ResponseHeadersRead). The named client
        // is configured with an infinite timeout for this reason.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.FetchTimeout);

        try
        {
            return await FetchCoreAsync(url, options.MaxFileSizeBytes, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FileFetchException(FileFetchError.Timeout,
                $"Timed out fetching the file after {options.FetchTimeout}.");
        }
    }

    private async Task<byte[]> FetchCoreAsync(string url, long maxBytes, CancellationToken ct)
    {
        var uri = ParseAndValidateUri(url);
        var http = _httpClientFactory.CreateClient(HttpClientName);

        for (var hop = 0; ; hop++)
        {
            await EnsureHostIsPublicAsync(uri, ct).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);

            HttpResponseMessage response;
            try
            {
                response = await http
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new FileFetchException(FileFetchError.HttpError, $"Failed to fetch '{uri}': {ex.Message}");
            }

            if (IsRedirect(response.StatusCode))
            {
                var location = response.Headers.Location;
                response.Dispose();

                if (location is null)
                    throw new FileFetchException(FileFetchError.HttpError, "Redirect response had no Location header.");
                if (hop >= MaxRedirects)
                    throw new FileFetchException(FileFetchError.HttpError, "Too many redirects.");

                // Resolve relative redirects against the current URI, then re-validate scheme + host.
                uri = ParseAndValidateUri((location.IsAbsoluteUri ? location : new Uri(uri, location)).ToString());
                continue;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new FileFetchException(FileFetchError.HttpError,
                        $"Fetching '{uri}' returned {(int)response.StatusCode} {response.StatusCode}.");
                }

                // Fast reject if the server advertises an over-limit size (don't trust it as authoritative).
                if (response.Content.Headers.ContentLength is long advertised && advertised > maxBytes)
                {
                    throw new FileFetchException(FileFetchError.TooLarge,
                        $"The file is {advertised} bytes, over the {maxBytes}-byte limit.");
                }

                return await ReadCappedAsync(response, maxBytes, ct).ConfigureAwait(false);
            }
        }
    }

    private static Uri ParseAndValidateUri(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new FileFetchException(FileFetchError.InvalidScheme, "The URL is not a valid absolute URI.");

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new FileFetchException(FileFetchError.InvalidScheme, "Only https:// URLs are allowed.");

        return uri;
    }

    private static async Task EnsureHostIsPublicAsync(Uri uri, CancellationToken ct)
    {
        IReadOnlyList<IPAddress> addresses;

        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.Host, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                throw new FileFetchException(FileFetchError.BlockedHost, $"Could not resolve host '{uri.Host}'.");
            }
        }

        // Check EVERY resolved address — partial defense against DNS rebinding where
        // a name resolves to one public and one private address.
        foreach (var address in addresses)
        {
            if (IsBlocked(address))
            {
                throw new FileFetchException(FileFetchError.BlockedHost,
                    $"Host '{uri.Host}' resolves to a non-public address ({address}) and was blocked.");
            }
        }
    }

    /// <summary>
    /// True when <paramref name="address"/> is not publicly routable and must not
    /// be fetched (loopback, private RFC1918, link-local, CGNAT, multicast, ULA).
    /// Internal for direct unit testing.
    /// </summary>
    internal static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] switch
            {
                0 => true,                                   // 0.0.0.0/8 "this network"
                10 => true,                                  // 10.0.0.0/8 private
                127 => true,                                 // loopback (also caught above)
                169 when b[1] == 254 => true,                // 169.254.0.0/16 link-local (cloud metadata)
                172 when b[1] is >= 16 and <= 31 => true,    // 172.16.0.0/12 private
                192 when b[1] == 168 => true,                // 192.168.0.0/16 private
                100 when b[1] is >= 64 and <= 127 => true,   // 100.64.0.0/10 CGNAT
                >= 224 => true,                              // 224.0.0.0/4 multicast + 240/4 reserved
                _ => false,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                return true;

            // Unique local addresses fc00::/7.
            var b = address.GetAddressBytes();
            return (b[0] & 0xFE) == 0xFC;
        }

        return true; // unknown address family → block by default
    }

    private static bool IsRedirect(HttpStatusCode status) => status is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Found or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private static async Task<byte[]> ReadCappedAsync(HttpResponseMessage response, long maxBytes, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;

        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new FileFetchException(FileFetchError.TooLarge,
                    $"The file exceeded the {maxBytes}-byte limit while downloading.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
