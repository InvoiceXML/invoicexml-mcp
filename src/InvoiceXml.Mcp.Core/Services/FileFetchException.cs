namespace InvoiceXml.Mcp.Core.Services;

/// <summary>Why a remote-file fetch was refused or failed.</summary>
public enum FileFetchError
{
    /// <summary>The URL was malformed or not an <c>https://</c> URL.</summary>
    InvalidScheme,

    /// <summary>The host resolved to a non-public address (loopback, private, link-local, ...).</summary>
    BlockedHost,

    /// <summary>The response body exceeded the configured size cap.</summary>
    TooLarge,

    /// <summary>The fetch did not complete within the configured timeout.</summary>
    Timeout,

    /// <summary>A transport-level or non-success HTTP response.</summary>
    HttpError,
}

/// <summary>
/// Thrown by <see cref="IRemoteFileFetcher"/> when a URL cannot be fetched,
/// whether for policy (SSRF guard) or transport reasons. The
/// <see cref="Error"/> category lets the calling tool produce a precise
/// <c>INPUT-URL</c> finding for the LLM.
/// </summary>
public sealed class FileFetchException : Exception
{
    public FileFetchException(FileFetchError error, string message) : base(message)
    {
        Error = error;
    }

    /// <summary>Category of the failure.</summary>
    public FileFetchError Error { get; }
}
