using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 invoice note (BG-1). The API accepts a note as a plain string or
/// as <c>{ note, subjectCode }</c>; the MCP schema always uses the object form
/// so the subject code stays discoverable.
/// </summary>
public sealed class InvoiceNote
{
    [Required(ErrorMessage = "Note text (BT-22) is required.")]
    [Description("The note text (BT-22). Required.")]
    public string? Note { get; set; }

    [Description(
        "Optional UNTDID 4451 subject code qualifying the note (BT-21), e.g. 'PMT' (recovery costs), " +
        "'PMD' (late-payment penalties), 'AAB' (discount terms), 'TXD' (tax statement). The French e-invoicing " +
        "rules (BR-FR) require the PMT, PMD and AAB mentions as coded notes.")]
    public string? SubjectCode { get; set; }

    /// <summary>Additional note fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
