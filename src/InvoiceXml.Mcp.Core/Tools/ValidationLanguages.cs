namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Shared wording of the optional <c>validationLanguage</c> setting, so every tool
/// and the create options explain it the same way.
/// </summary>
internal static class ValidationLanguages
{
    /// <summary>Description of the setting itself, without any per-tool scope.</summary>
    public const string Description =
        "Language of the validation findings: 'en' (default), 'de' or 'fr'. Words the 'message' of every error " +
        "and warning; rule ids, BT codes and fields stay the same. Set 'de' or 'fr' when the user writes in " +
        "German or French, so the findings can be shown to them as they are; leave it out otherwise.";

    /// <summary>Tool-parameter description for tools that validate and return findings.</summary>
    public const string ParameterDescription = "Optional. " + Description;
}
