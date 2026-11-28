using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Features.Accounting;
using NEXT.Domain.Enums;
using NEXT.web.Localization;

namespace NEXT.web.Services;

public sealed class AccountingText(IStringLocalizer<UiText> text)
{
    private static readonly ResourceManager Resources = new("NEXT.web.Resources.Localization.UiText", typeof(UiText).Assembly);
    private static readonly CultureInfo[] Cultures = [CultureInfo.GetCultureInfo("ar"), CultureInfo.GetCultureInfo("en")];

    public string Description(AccountTransactionDto item) => AccountingDescriptions.Template(item) is { } key
        ? text[key, item.InvoiceNumber!].Value : item.Description is AccountingDescriptions.GoodsReceipt or AccountingDescriptions.InventoryOpening or AccountingDescriptions.OpeningAdoption or "AccountingDescriptionDebtReceipt"
        ? text[item.Description].Value : item.Description;

    public AccountingSearchMatches Search(string? value)
    {
        var term = value?.Trim();
        if (string.IsNullOrWhiteSpace(term))
            return new([], [], []);
        bool Matches(string key) => Cultures.Any(culture => Resources.GetString(key, culture)?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
        return new(Enum.GetValues<LedgerEntryKind>().Where(kind => Matches("AccountingEntry" + kind) || (kind switch
        {
            LedgerEntryKind.Receipt => Matches(AccountingDescriptions.InvoiceReceipt),
            LedgerEntryKind.SalesRecognition => Matches(AccountingDescriptions.InvoiceSale),
            LedgerEntryKind.InvoiceCancellation => Matches(AccountingDescriptions.InvoiceCancellation),
            LedgerEntryKind.Refund => Matches(AccountingDescriptions.InvoiceRefund),
            LedgerEntryKind.OpeningBalance => Matches(AccountingDescriptions.OpeningAdoption),
            LedgerEntryKind.GoodsReceipt => Matches(AccountingDescriptions.GoodsReceipt) || Matches(AccountingDescriptions.InventoryOpening),
            _ => false
        })).ToArray(),
            Enum.GetValues<PaymentPurpose>().Where(purpose => Matches("AccountingPurpose" + purpose)).ToArray(),
            Enum.GetValues<PaymentMethod>().Where(method => Matches(method.ToString())).ToArray());
    }
}
