using System.ComponentModel.DataAnnotations;

namespace InvoiceXml.Mcp.Core.Options;

/// <summary>
/// Limits for the URL-fetch input mode of the validation tools. Bound from the
/// <c>Mcp:FileInput</c> configuration section; sensible defaults apply when the
/// section is absent.
/// </summary>
public sealed class FileInputOptions
{
    /// <summary>Configuration section name: <c>Mcp:FileInput</c>.</summary>
    public const string SectionName = "Mcp:FileInput";

    /// <summary>
    /// Maximum size, in bytes, of a file the server will download from a URL.
    /// Defaults to 5 MB.
    /// </summary>
    [Range(1, 104_857_600)] // 1 byte .. 100 MB hard ceiling
    public long MaxFileSizeBytes { get; set; } = 5L * 1024 * 1024;

    /// <summary>Overall timeout for a single URL fetch (connect + headers + body). Defaults to 30 seconds.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
