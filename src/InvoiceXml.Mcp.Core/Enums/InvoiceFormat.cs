using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// E-invoicing formats supported by the InvoiceXML API's <c>/create</c> family.
/// Wire / URL values are lower-case slugs.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InvoiceFormat>))]
public enum InvoiceFormat
{
    /// <summary>UBL 2.1, Peppol BIS Billing 3.0 (EN 16931). XML output.</summary>
    [JsonStringEnumMemberName("ubl")]
    Ubl,

    /// <summary>UN/CEFACT Cross Industry Invoice (CII). XML output.</summary>
    [JsonStringEnumMemberName("cii")]
    Cii,

    /// <summary>XRechnung (German public sector, CIUS over EN 16931). XML output.</summary>
    [JsonStringEnumMemberName("xrechnung")]
    XRechnung,

    /// <summary>Factur-X (French / German hybrid PDF/A-3 with embedded CII XML). PDF output.</summary>
    [JsonStringEnumMemberName("facturx")]
    FacturX,

    /// <summary>ZUGFeRD (German hybrid PDF/A-3 with embedded CII XML). PDF output.</summary>
    [JsonStringEnumMemberName("zugferd")]
    Zugferd,
}

/// <summary>Subset of <see cref="InvoiceFormat"/> whose output is plain XML.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<XmlInvoiceFormat>))]
public enum XmlInvoiceFormat
{
    [JsonStringEnumMemberName("ubl")] Ubl,
    [JsonStringEnumMemberName("cii")] Cii,
    [JsonStringEnumMemberName("xrechnung")] XRechnung,
}

/// <summary>Subset of <see cref="InvoiceFormat"/> whose output is a hybrid PDF/A-3.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PdfInvoiceFormat>))]
public enum PdfInvoiceFormat
{
    [JsonStringEnumMemberName("facturx")] FacturX,
    [JsonStringEnumMemberName("zugferd")] Zugferd,
}
