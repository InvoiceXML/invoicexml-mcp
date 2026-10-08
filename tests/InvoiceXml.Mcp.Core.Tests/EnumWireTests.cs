using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Tests;

/// <summary>
/// <see cref="EnumWire"/> is the one place a wire spelling is derived; these
/// pin the members whose slug is not simply the lower-cased member name.
/// </summary>
public class EnumWireTests
{
    [Fact]
    public void Slug_UsesTheJsonStringEnumMemberName()
    {
        Assert.Equal("order-x", EnumWire.Slug(ValidationReportFormat.OrderX));
        Assert.Equal("order-x", EnumWire.Slug(OrderFormat.OrderX));
        Assert.Equal("cio", EnumWire.Slug(OrderFormat.Cio));
        Assert.Equal("br-fr", EnumWire.Slug(ExtraRuleset.BrFr));
        Assert.Equal("none", EnumWire.Slug(FooterBrand.None));
        Assert.Equal("xrechnung", EnumWire.Slug(ValidationReportFormat.XRechnung));
    }
}
