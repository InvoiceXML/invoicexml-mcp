using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoiceXml.Mcp.Core.Models;

/// <summary>
/// EN 16931 document-level totals (BG-22). Property names track the API's
/// <c>DocumentTotals</c> exactly: <c>sumOfLineNetAmounts</c>, <c>taxBasisTotalAmount</c>,
/// <c>grandTotalAmount</c>, <c>duePayableAmount</c>, ... A previous version of
/// this DTO used UBL-style names (<c>lineExtensionAmount</c>, ...) and silently
/// broke deserialization; do not rename these without verifying the API model.
/// </summary>
/// <remarks>
/// The API doesn't apply <c>[Required]</c> at the C# level on any of these,
/// even though the standard makes BT-106/109/112/115 mandatory: validation
/// happens downstream in XSD / Schematron. Stay permissive here for the same
/// reason and let those layers surface precise errors.
/// </remarks>
public sealed class DocumentTotals
{
    [Description("Sum of all line net amounts (BT-106). Mandatory under EN 16931.")]
    public decimal? SumOfLineNetAmounts { get; set; }

    [Description("Sum of document-level allowances (BT-107). Optional.")]
    public decimal? SumOfAllowances { get; set; }

    [Description("Sum of document-level charges (BT-108). Optional.")]
    public decimal? SumOfCharges { get; set; }

    [Description("Invoice total amount without VAT (BT-109). Mandatory under EN 16931.")]
    public decimal? TaxBasisTotalAmount { get; set; }

    [Description("Invoice total VAT amount (BT-110). Optional at the C# level; required when VAT applies.")]
    public decimal? TaxTotalAmount { get; set; }

    [Description("Invoice total amount with VAT (BT-112). Mandatory under EN 16931.")]
    public decimal? GrandTotalAmount { get; set; }

    [Description("Amount due for payment (BT-115). Mandatory under EN 16931.")]
    public decimal? DuePayableAmount { get; set; }

    /// <summary>Additional total fields (TaxTotalAmountInAccountingCurrency, PaidAmount, RoundingAmount, ...) flow through.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Additional { get; set; }
}
