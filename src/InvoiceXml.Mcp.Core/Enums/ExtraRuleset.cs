using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// Extra national rule sets the API can apply on top of the pipeline the
/// document's declared profile selects. Wire values match the API's
/// <c>rules</c> field.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExtraRuleset>))]
public enum ExtraRuleset
{
    /// <summary>The French e-invoicing mandate's BR-FR rules.</summary>
    [JsonStringEnumMemberName("br-fr")] BrFr,
}
