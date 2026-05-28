using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceXml.Mcp.Core.Enums;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 BT-first invoice document. This is a <strong>hybrid mirror</strong>
/// of the API's <c>InvoiceDocument</c>: the fields LLMs use most often are
/// modelled explicitly so the generated MCP schema gives strong autocomplete
/// and structural validation; every other field flows through via
/// <see cref="Additional"/> without code changes here.
/// </summary>
/// <remarks>
/// <para>
/// Property names <strong>must</strong> match what the API's JSON deserializer
/// expects (camelCase after <c>JsonNamingPolicy.CamelCase</c>). A previous
/// iteration used UBL-style names that silently bypassed the API; do not
/// rename these without checking the corresponding API model first.
/// </para>
/// <para>
/// <c>[Required]</c> attributes mirror the API's own <c>[Required]</c> set.
/// Other EN 16931-mandatory fields (Totals, VatBreakdowns, ...) are left
/// optional at this layer because the API enforces them downstream via
/// XSD / Schematron, producing more precise error messages than client-side
/// data-annotation validation would.
/// </para>
/// </remarks>
public sealed class InvoiceDocument
{
    [Required(ErrorMessage = "Invoice number (BT-1) is required.")]
    [Description("Invoice number (BT-1). Required. Must be unique within the seller's sequence.")]
    public string? InvoiceNumber { get; set; }

    [Description("Invoice issue date (BT-2) in ISO 8601 (yyyy-MM-dd). Optional: defaults to today server-side if omitted.")]
    public DateOnly? IssueDate { get; set; }

    [Description("Payment due date (BT-9) in ISO 8601 (yyyy-MM-dd). Optional.")]
    public DateOnly? DueDate { get; set; }

    [Description("UNTDID 1001 invoice type code (BT-3). Optional: defaults to 380 (Commercial invoice) server-side if omitted.")]
    public InvoiceTypeCode? TypeCode { get; set; }

    [Required(ErrorMessage = "Invoice currency code (BT-5) is required.")]
    [Description("Invoice currency code, ISO 4217 (BT-5). Required. Example: 'EUR', 'USD', 'GBP'.")]
    public string? Currency { get; set; }

    [Description("VAT accounting currency code (BT-6). Optional, only set when different from the invoice currency.")]
    public string? TaxCurrency { get; set; }

    [Description("Specification identifier (BT-24), e.g. 'urn:cen.eu:en16931:2017'. Optional: defaults are applied per format server-side.")]
    public string? SpecificationId { get; set; }

    [Description("Buyer reference (BT-10). Carries the Leitweg-ID in XRechnung. Optional.")]
    public string? BuyerReference { get; set; }

    [Description("Purchase order reference (BT-13). Optional.")]
    public string? PurchaseOrderReference { get; set; }

    [Required(ErrorMessage = "Seller (BG-4) is required.")]
    [Description("Seller party (BG-4). Required.")]
    public Party? Seller { get; set; }

    [Required(ErrorMessage = "Buyer (BG-7) is required.")]
    [Description("Buyer party (BG-7). Required.")]
    public Party? Buyer { get; set; }

    [Required(ErrorMessage = "Invoice must contain at least one line item (BR-16).")]
    [MinLength(1, ErrorMessage = "Invoice must contain at least one line item (BR-16).")]
    [Description("Invoice line items (BG-25). At least one is required.")]
    public List<InvoiceLine>? Lines { get; set; }

    [Description("Document-level totals (BG-22). Mandatory under EN 16931; validated server-side.")]
    public DocumentTotals? Totals { get; set; }

    [Description("VAT breakdown by category and rate (BG-23). Mandatory under EN 16931; validated server-side.")]
    public List<VatBreakdown>? VatBreakdowns { get; set; }

    /// <summary>
    /// Any invoice field not explicitly modelled above (<c>notes</c>, <c>precedingInvoiceReferences</c>,
    /// <c>delivery</c>, <c>invoicingPeriod</c>, <c>paymentDetails</c>, <c>allowances</c>, <c>charges</c>,
    /// <c>businessProcessType</c>, ...) flows through to the API verbatim via this dictionary.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
