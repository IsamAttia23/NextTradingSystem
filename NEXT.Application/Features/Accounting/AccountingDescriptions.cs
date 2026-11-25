using NEXT.Application.DTOs.Accounting;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Accounting;

// Generated descriptions store stable keys; historical text is translated only when it matches a known template.
public static class AccountingDescriptions
{
    public const string OpeningAdoption = "AccountingDescriptionOpeningAdoption";
    public const string GoodsReceipt = "AccountingDescriptionGoodsReceipt";
    public const string InventoryOpening = "AccountingDescriptionInventoryOpening";
    public const string InvoiceReceipt = "AccountingDescriptionInvoiceReceipt";
    public const string InvoiceSale = "AccountingDescriptionInvoiceSale";
    public const string InvoiceCancellation = "AccountingDescriptionInvoiceCancellation";
    public const string InvoiceRefund = "AccountingDescriptionInvoiceRefund";

    public static string? Template(AccountTransactionDto item)
    {
        if (item.InvoiceId is null || string.IsNullOrWhiteSpace(item.InvoiceNumber)) return null;
        var (key, legacy) = item.Kind switch
        {
            LedgerEntryKind.Receipt => (InvoiceReceipt, "سداد من قيمة فاتورة رقم "),
            LedgerEntryKind.SalesRecognition => (InvoiceSale, "قيمة فاتورة مبيعات رقم "),
            LedgerEntryKind.InvoiceCancellation => (InvoiceCancellation, "رصيد مستحق للرد عن إلغاء الفاتورة "),
            LedgerEntryKind.Refund => (InvoiceRefund, "رد مبلغ عن الفاتورة الملغاة "),
            _ => (null, null)
        };
        return key is not null && (item.Description == key || item.Description == legacy + item.InvoiceNumber) ? key : null;
    }
}
