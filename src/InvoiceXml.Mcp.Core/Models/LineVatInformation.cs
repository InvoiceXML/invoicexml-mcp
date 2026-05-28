using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 line VAT information group (BG-30). Mirrors the API's
/// <c>LineVatInformation</c>.
/// </summary>
/// <remarks>
/// The API marks <c>CategoryCode</c> as mandatory at the spec level but
/// deliberately does <em>not</em> apply <c>[Required]</c> to it because
/// BT-152 (Rate) has cross-field rules depending on the category that are
/// enforced downstream. We follow the same pattern for consistency.
/// </remarks>
public sealed class LineVatInformation
{
    [Description("VAT category for this line (BT-151). Required by the standard.")]
    public VatCategoryCode? CategoryCode { get; set; }

    [Description("VAT rate applied to this line, as a percentage (BT-152). Required when CategoryCode requires a non-zero rate.")]
    public decimal? Rate { get; set; }

    /// <summary>Additional line VAT fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
