using InvoiceXml.Mcp.Core.Models;
using InvoiceXml.Mcp.Core.Services;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Shared input-arbitration helpers for the validation tools, which each accept
/// two mutually exclusive content modes: inline (base64 bytes or XML text) and a
/// URL the server fetches. The inline mode differs per tool and is handled by the
/// caller; the URL mode resolves to bytes here and is shared. Failures are returned
/// as a ready-to-emit <see cref="ValidationResult"/> with a single <c>INPUT-…</c>
/// finding, so the tool never throws on bad input.
/// </summary>
internal static class FileInputResolver
{
    /// <summary>
    /// Agent guidance on when base64 is a safe way to send a file, shared so every tool says
    /// the same. Base64 that a model writes out character by character gets corrupted beyond a
    /// few tens of KB. An agent that calls tools from code passes the bytes of the real file
    /// intact at any size, so only the written-out case is limited.
    /// </summary>
    public const string Base64Guidance =
        "Base64 is reliable at any size when code sends it: if you call tools from a script or sandbox that holds " +
        "the file, read the real bytes there and pass the base64 as a value. Only write base64 out yourself for a " +
        "small file (a few tens of KB); longer base64 written out by a model gets corrupted, so use a URL then.";

    /// <summary>The short form of <see cref="Base64Guidance"/> for the description of a base64 parameter.</summary>
    public const string Base64ParameterNote =
        "any size when code sends the real file's bytes; written out by you, small files only";

    /// <summary>
    /// Verifies that exactly one of the supplied modes is set. Returns
    /// <see langword="null"/> when exactly one is present, otherwise a
    /// <see cref="ValidationResult"/> describing the problem.
    /// </summary>
    public static ValidationResult? ValidateExactlyOne((string Name, bool Provided)[] modes)
    {
        var count = modes.Count(m => m.Provided);
        if (count == 1)
            return null;

        var names = string.Join(", ", modes.Select(m => m.Name));
        var fields = modes.Select(m => m.Name).ToArray();

        return count == 0
            ? InputError("INPUT-REQUIRED", $"Provide exactly one of: {names}.", fields)
            : InputError("INPUT-EXCLUSIVE", $"Provide only one of: {names}; you set {count}.", fields);
    }

    /// <summary>Fetches a URL to bytes, mapping fetch failures to an <c>INPUT-URL</c> finding.</summary>
    public static async Task<(byte[]? Bytes, ValidationResult? Error)> FetchUrlAsync(
        IRemoteFileFetcher fetcher, string url, string fieldName, CancellationToken ct)
    {
        try
        {
            var bytes = await fetcher.FetchAsync(url, ct).ConfigureAwait(false);
            return (bytes, null);
        }
        catch (FileFetchException ex)
        {
            return (null, InputError("INPUT-URL", ex.Message, [fieldName]));
        }
    }

    /// <summary>Builds a <see cref="ValidationResult"/> carrying a single input-level finding.</summary>
    public static ValidationResult InputError(string rule, string message, IReadOnlyList<string> fields) => new()
    {
        Valid = false,
        Detail = message,
        Errors = [new ValidationFinding { Rule = rule, Message = message, Fields = fields }],
        Warnings = [],
    };
}
