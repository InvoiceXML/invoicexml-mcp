using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 preceding invoice reference (BG-3). Mirrors the API's
/// <c>PrecedingInvoiceReference</c>.
/// </summary>
public sealed class PrecedingInvoiceReference
{
    [Required(ErrorMessage = "Preceding invoice reference (BT-25) is required.")]
    [Description("Number of the invoice being credited or corrected (BT-25). Required.")]
    public string? Reference { get; set; }

    [Description("Issue date of that invoice (BT-26) in ISO 8601 (yyyy-MM-dd). Optional.")]
    public DateOnly? IssueDate { get; set; }

    /// <summary>Additional reference fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
