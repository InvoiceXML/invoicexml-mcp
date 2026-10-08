using System.Text.Json;
using InvoiceXml.Mcp.Core.Tools;
using ModelContextProtocol.Server;

namespace InvoiceXml.Mcp.Core.Tests.Tools;

/// <summary>
/// Guards what agents actually see: the JSON input schema the MCP SDK
/// advertises for each tool. A field the schema does not list is invisible to
/// the model even when the API would accept it.
/// </summary>
public class ToolSchemaTests
{
    [Theory]
    [InlineData("invoice.paymentDetails.paymentAccountIdentifier")]
    [InlineData("invoice.paymentDetails.paymentMeansCode")]
    [InlineData("invoice.paymentDetails.paymentTerms")]
    [InlineData("invoice.notes.subjectCode")]
    [InlineData("invoice.precedingInvoiceReferences.reference")]
    [InlineData("invoice.supportingDocuments.externalUri")]
    [InlineData("invoice.delivery.deliveryAddress.country")]
    [InlineData("invoice.invoicingPeriod.startDate")]
    [InlineData("invoice.allowances.vatCategoryCode")]
    [InlineData("invoice.charges.reasonCode")]
    [InlineData("invoice.seller.electronicAddress.schemeId")]
    [InlineData("invoice.seller.contact.email")]
    [InlineData("invoice.seller.taxRegistrationIdentifier")]
    [InlineData("invoice.seller.legalRegistration.identifier")]
    [InlineData("invoice.buyer.electronicAddress.identifier")]
    [InlineData("invoice.buyer.identifiers.schemeId")]
    [InlineData("invoice.lines.linePeriod.endDate")]
    [InlineData("invoice.lines.allowances.amount")]
    [InlineData("invoice.lines.objectIdentifier.identifier")]
    [InlineData("invoice.lines.item.standardIdentifier.schemeId")]
    [InlineData("invoice.lines.item.attributes.value")]
    [InlineData("invoice.totals.paidAmount")]
    [InlineData("options.rules")]
    [InlineData("options.syntax")]
    [InlineData("options.logoUrl")]
    [InlineData("options.footerBrand")]
    [InlineData("options.language")]
    public void CreateInvoice_SchemaAdvertisesField(string path)
    {
        var schema = InputSchema<CreateInvoiceTool>(nameof(CreateInvoiceTool.CreateInvoiceAsync));

        Assert.True(TryResolve(schema, path, out _), $"create_invoice schema does not expose '{path}'.");
    }

    [Fact]
    public void CreateInvoice_SchemaNoLongerAdvertisesRemovedXRechnungVersion()
    {
        var schema = InputSchema<CreateInvoiceTool>(nameof(CreateInvoiceTool.CreateInvoiceAsync));

        Assert.False(TryResolve(schema, "options.version", out _));
    }

    [Fact]
    public void CreateInvoice_FooterBrandSchemaOffersOnlyInvoiceXmlAndNone()
    {
        var schema = InputSchema<CreateInvoiceTool>(nameof(CreateInvoiceTool.CreateInvoiceAsync));

        Assert.True(TryResolve(schema, "options.footerBrand", out var footerBrand));
        var values = EnumValues(footerBrand).Order().ToArray();
        Assert.Equal(["invoicexml", "none"], values);
    }

    [Theory]
    [InlineData(typeof(CreateInvoiceTool), nameof(CreateInvoiceTool.CreateInvoiceAsync))]
    [InlineData(typeof(ValidatePdfInvoiceTool), nameof(ValidatePdfInvoiceTool.ValidatePdfAsync))]
    [InlineData(typeof(EmbedInvoiceTool), nameof(EmbedInvoiceTool.EmbedInvoiceAsync))]
    [InlineData(typeof(RenderValidationReportTool), nameof(RenderValidationReportTool.RenderValidationReportAsync))]
    public void FacturXCapableTools_TellTheAgentToApplyBrFrByDefault(Type toolType, string methodName)
    {
        var method = toolType.GetMethod(methodName)!;
        var description = McpServerTool.Create(method, toolType).ProtocolTool.Description;

        Assert.Contains(ExtraRules.FacturXDefaultGuidance, description);
    }

    internal static JsonElement InputSchema<TTool>(string methodName)
    {
        var method = typeof(TTool).GetMethod(methodName)
            ?? throw new InvalidOperationException($"{typeof(TTool).Name}.{methodName} not found.");
        var tool = McpServerTool.Create(method, typeof(TTool));
        return tool.ProtocolTool.InputSchema;
    }

    /// <summary>
    /// Walks a dotted path through <c>properties</c>, stepping into array
    /// <c>items</c>, nullable <c>anyOf</c>/<c>oneOf</c> branches and the
    /// <c>$ref</c> JSON pointers the exporter emits for a type it already
    /// described elsewhere in the schema.
    /// </summary>
    internal static bool TryResolve(JsonElement schema, string path, out JsonElement node)
    {
        node = schema;
        foreach (var segment in path.Split('.'))
        {
            if (!TryStepInto(schema, node, segment, out node))
                return false;
        }
        return true;
    }

    internal static IEnumerable<string> EnumValues(JsonElement node)
    {
        if (node.TryGetProperty("enum", out var values))
        {
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind == JsonValueKind.String)
                    yield return value.GetString()!;
            }
        }

        foreach (var keyword in new[] { "anyOf", "oneOf" })
        {
            if (!node.TryGetProperty(keyword, out var branches))
                continue;
            foreach (var branch in branches.EnumerateArray())
            {
                foreach (var value in EnumValues(branch))
                    yield return value;
            }
        }
    }

    private static bool TryStepInto(JsonElement root, JsonElement node, string name, out JsonElement child)
    {
        node = Dereference(root, node);

        if (node.TryGetProperty("properties", out var properties) && properties.TryGetProperty(name, out child))
            return true;

        if (node.TryGetProperty("items", out var items) && TryStepInto(root, items, name, out child))
            return true;

        foreach (var keyword in new[] { "anyOf", "oneOf" })
        {
            if (!node.TryGetProperty(keyword, out var branches))
                continue;
            foreach (var branch in branches.EnumerateArray())
            {
                if (TryStepInto(root, branch, name, out child))
                    return true;
            }
        }

        child = default;
        return false;
    }

    private static JsonElement Dereference(JsonElement root, JsonElement node)
    {
        while (node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty("$ref", out var reference)
            && reference.GetString() is { } pointer
            && pointer.StartsWith("#/", StringComparison.Ordinal))
        {
            var target = root;
            foreach (var token in pointer[2..].Split('/'))
            {
                var key = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                target = int.TryParse(key, out var index) && target.ValueKind == JsonValueKind.Array
                    ? target[index]
                    : target.GetProperty(key);
            }
            node = target;
        }
        return node;
    }
}
