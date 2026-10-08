using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 additional supporting document (BG-24). Mirrors the API's
/// <c>AdditionalSupportingDocument</c>.
/// </summary>
public sealed class SupportingDocument
{
    [Required(ErrorMessage = "Supporting document reference (BT-122) is required.")]
    [Description("Identifier of the supporting document (BT-122), e.g. a timesheet or delivery note number. Required.")]
    public string? Reference { get; set; }

    [Description(
        "UNTDID 1001 document type code: '916' related document (default), '50' tender or lot reference (BT-17), " +
        "'130' invoiced object identifier (BT-18).")]
    public string? DocumentTypeCode { get; set; }

    [Description("Scheme of the reference (BT-18-1). Only meaningful with documentTypeCode '130'.")]
    public string? SchemeId { get; set; }

    [Description("Description of the supporting document (BT-123).")]
    public string? Description { get; set; }

    [Description("Public URL where the document can be downloaded (BT-124). Prefer this over embedding the file.")]
    public string? ExternalUri { get; set; }

    [Description(
        "The document itself, base64 encoded (BT-125). Small files only, and only the real bytes of a file you " +
        "actually have; never synthesize content. Requires attachmentMimeCode and attachmentFilename.")]
    public string? Attachment { get; set; }

    [Description("MIME type of the attached document (BT-125-1): 'application/pdf', 'image/png', 'image/jpeg', 'text/csv', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' or 'application/vnd.oasis.opendocument.spreadsheet'.")]
    public string? AttachmentMimeCode { get; set; }

    [Description("File name of the attached document (BT-125-2), e.g. 'timesheet.pdf'.")]
    public string? AttachmentFilename { get; set; }

    /// <summary>Additional fields flow through to the API unchanged.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
