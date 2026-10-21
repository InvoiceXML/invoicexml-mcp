using System.Buffers.Text;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Shared handling of the logo sent as the image itself (<c>logo</c>, the PNG
/// or JPEG file as base64) next to <c>logoUrl</c>. The API refuses a logo that
/// is not plain base64 or that comes with a <c>logoUrl</c> as a 4002, and the
/// create routes bill even that 400, so the tools turn such a request down up
/// front, naming the field to fix.
/// </summary>
internal static class InlineLogo
{
    /// <summary>Tool-parameter description, shared so every tool explains the option the same way.</summary>
    public const string ParameterDescription =
        "The logo as the PNG or JPEG file itself, in plain base64 without a data: prefix (max 2 MB decoded " +
        "and 4 megapixels), printed top-left exactly as 'logoUrl' would be. Use it for a logo that is not at a " +
        "public URL. Set 'logo' or 'logoUrl', not both.";

    /// <summary>
    /// Returns <see langword="null"/> when the logo passes (or none was sent),
    /// otherwise the message for an <c>INPUT-LOGO</c> error on <paramref name="prefix"/><c>logo</c>.
    /// </summary>
    public static string? Refused(string? logo, string? logoUrl, string prefix)
    {
        if (string.IsNullOrWhiteSpace(logo))
            return null;

        if (!Base64.IsValid(logo))
        {
            return $"'{prefix}logo' is not plain base64. Pass the PNG or JPEG file as standard base64 without a " +
                   $"data: prefix, or pass a public https:// URL via '{prefix}logoUrl' instead.";
        }

        if (!string.IsNullOrWhiteSpace(logoUrl))
            return $"Send the logo either as '{prefix}logo' or as '{prefix}logoUrl', not both.";

        return null;
    }
}
