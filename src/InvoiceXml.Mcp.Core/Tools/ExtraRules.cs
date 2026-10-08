using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Tools;

/// <summary>
/// Shared handling of the optional <c>rules</c> tool parameter (extra national
/// rule sets such as BR-FR). Several API routes simply do not bind the field,
/// so a rule set sent there would be skipped without a word; the tools refuse
/// it up front instead, so an agent never reports a compliance check that did
/// not run.
/// </summary>
internal static class ExtraRules
{
    /// <summary>
    /// Agent guidance for Factur-X: nearly every Factur-X invoice travels over the
    /// French e-invoicing network, whose platforms reject BR-FR failures, so the
    /// agent applies <c>br-fr</c> by default and drops it only on an explicit no.
    /// </summary>
    public const string FacturXDefaultGuidance =
        "Factur-X invoices almost always travel over the French e-invoicing network (a PDP / Plateforme Agréée), " +
        "which rejects invoices that fail the BR-FR rules. So for format 'facturx', set rules to ['br-fr'] by " +
        "default and tell the user you did; leave it out only when the user explicitly says the invoice will not " +
        "be sent over that network. If BR-FR findings come back, fix them like any other error.";

    /// <summary>Agent guidance for the syntaxes (UBL, CII) where BR-FR is opt-in.</summary>
    public const string AskForFrenchNetworkGuidance =
        "For a French domestic invoice, ask the user whether it will be sent over the French e-invoicing network " +
        "(a PDP / Plateforme Agréée); if yes, set rules to ['br-fr'].";

    /// <summary>Tool-parameter description, shared so every tool explains the option the same way.</summary>
    public const string ParameterDescription =
        "Optional extra national rule sets to check on top of the format's own rules. Allowed value: 'br-fr' " +
        "(the French e-invoicing mandate's BR-FR rules, which the French platforms apply to every domestic " +
        "invoice; needs at least the BASIC profile on CII-based documents). Violations come back as ordinary " +
        "findings with BR-FR-* rule ids.";

    /// <summary>
    /// Returns <see langword="null"/> when <paramref name="rules"/> is empty or the
    /// format supports it, otherwise the message for an <c>INPUT-RULES</c> error.
    /// </summary>
    public static string? Unsupported(IReadOnlyList<ExtraRuleset>? rules, string formatSlug, params string[] supportedSlugs)
    {
        if (rules is not { Count: > 0 } || supportedSlugs.Contains(formatSlug, StringComparer.Ordinal))
            return null;

        return $"The 'rules' option is not available for format '{formatSlug}'. " +
               $"It is supported for: {string.Join(", ", supportedSlugs)}. Remove 'rules' or pick a supported format.";
    }
}
