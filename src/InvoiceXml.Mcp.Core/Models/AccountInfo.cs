using System.ComponentModel;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// The caller's plan and credit balance, as returned by <c>GET /v1/me</c>.
/// Mirrors the API's <c>MeResponse</c>.
/// </summary>
public sealed class AccountInfo
{
    [Description("Plan of the account: 'subscription' or 'free'.")]
    public string? Plan { get; init; }

    [Description("Credits still available in the current period. Each billable tool call uses one.")]
    public int? CreditsRemaining { get; init; }

    [Description("Credits granted for the current period.")]
    public int? CreditsTotal { get; init; }

    [Description("Credits used in the current period.")]
    public int? CreditsConsumed { get; init; }

    [Description("Credits used over the lifetime of the account.")]
    public int? CreditsConsumedTotal { get; init; }
}
