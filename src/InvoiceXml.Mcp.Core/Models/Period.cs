using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// A date range: the invoicing period (BG-14, BT-73/BT-74) or an invoice line
/// period (BG-26, BT-134/BT-135). The API's <c>InvoicingPeriod</c> and
/// <c>InvoiceLinePeriod</c> share this exact wire shape.
/// </summary>
public sealed class Period
{
    [Description("Start date in ISO 8601 (yyyy-MM-dd). Supply at least one of startDate / endDate.")]
    public DateOnly? StartDate { get; set; }

    [Description("End date in ISO 8601 (yyyy-MM-dd). Supply at least one of startDate / endDate.")]
    public DateOnly? EndDate { get; set; }

    /// <summary>Additional period fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
