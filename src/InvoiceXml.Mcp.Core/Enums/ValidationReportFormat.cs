using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// Documents <c>render_validation_report</c> accepts: the invoice formats plus
/// Order-X. Wire values match the API's <c>/v1/validate/{format}/report</c> slugs.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ValidationReportFormat>))]
public enum ValidationReportFormat
{
    [JsonStringEnumMemberName("ubl")] Ubl,
    [JsonStringEnumMemberName("cii")] Cii,
    [JsonStringEnumMemberName("xrechnung")] XRechnung,
    [JsonStringEnumMemberName("facturx")] FacturX,
    [JsonStringEnumMemberName("zugferd")] Zugferd,

    /// <summary>An Order-X purchase order: hybrid PDF or plain CIO XML.</summary>
    [JsonStringEnumMemberName("order-x")] OrderX,
}
