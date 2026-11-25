using Microsoft.Extensions.Localization;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.QueryModels;
using NEXT.web.Localization;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class FinancialAccountsPresenter(IAccountingReadService reads, TablePresenter tables, IStringLocalizer<UiText> text, AccountingText accountingText)
{
    public async Task<TableModel> MovementsAsync(AccountingQuery query, Func<string, object?, string> route, CancellationToken ct)
    {
        var page = await reads.GetTransactionsAsync(query, ct, accountingText.Search(query.SearchTerm));
        var model = await tables.BuildAsync("AccountMovements", ["Date", "Description", "CustomerName", "AccountingDebit", "AccountingCredit",
            "PaymentNumber", "PaymentMethod", "AccountingOperationType", "InvoicePaymentRecordedBy"], page, query, item =>
        {
            IReadOnlyDictionary<int, TableCellLink>? links = item.InvoiceId is { } id && !string.IsNullOrWhiteSpace(item.InvoiceNumber)
                ? new Dictionary<int, TableCellLink> { [1] = new(item.InvoiceNumber, route("Details", new { controller = "Invoices", id })) } : null;
            return new TableRow([item.Date.ToString("yyyy-MM-dd"), accountingText.Description(item), item.CustomerName ?? "—",
                item.Debit.ToString("N2") + " SDG", item.Credit.ToString("N2") + " SDG", item.PaymentNumber ?? "—",
                item.PaymentMethod is { } method ? text[method.ToString()].Value : "—",
                text[item.Purpose is null or NEXT.Domain.Enums.PaymentPurpose.Unspecified
                    ? "AccountingEntry" + item.Kind : "AccountingPurpose" + item.Purpose].Value, item.ActorName], [], CellLinks: links);
        }, [], ct);
        return model with { CssClass = "financial-movements", ShowActions = false, SectionLabel = "FinanceGroup",
            Title = query.Account.HasValue ? "FinancialAccount" + query.Account.Value : "AccountMovements",
            Description = "AccountMovementsDescription", NumericColumns = ["AccountingDebit", "AccountingCredit"] };
    }
}
