using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Enums;

/// <summary>
/// Output of the Order-X create family. Wire values match the API's route
/// slugs: <c>/v1/create/cio</c> and <c>/v1/create/order-x</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderFormat>))]
public enum OrderFormat
{
    /// <summary>Plain UN/CEFACT Cross-Industry Order XML.</summary>
    [JsonStringEnumMemberName("cio")] Cio,

    /// <summary>Order-X hybrid PDF/A-3 with the CIO XML embedded.</summary>
    [JsonStringEnumMemberName("order-x")] OrderX,
}

/// <summary>Order-X conformance profile. Wire values match the API's <c>options.profile</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderProfile>))]
public enum OrderProfile
{
    [JsonStringEnumMemberName("basic")] Basic,
    [JsonStringEnumMemberName("comfort")] Comfort,
    [JsonStringEnumMemberName("extended")] Extended,
}

/// <summary>UNTDID 1001 order document type. Wire values match the API's <c>options.typeCode</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderTypeCode>))]
public enum OrderTypeCode
{
    /// <summary>Purchase order (220).</summary>
    [JsonStringEnumMemberName("220")] Order,

    /// <summary>Order change (230).</summary>
    [JsonStringEnumMemberName("230")] OrderChange,

    /// <summary>Order response (231).</summary>
    [JsonStringEnumMemberName("231")] OrderResponse,
}
