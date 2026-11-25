using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Accounting;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
namespace NEXT.Application.Features.Accounting;

public sealed class AccountingReconciliation(IReadRepository<Stock> stocks, IReadRepository<Invoice> invoices,
    IReadRepository<DebtItem> debts, IReadRepository<AccountTransaction> lines, IReadRepository<Payment> payments,
    IQueryExecutor executor, IUnitOfWork work)
{
    public Task<IReadOnlyList<ReconciliationRow>> ReadAsync(CancellationToken ct) => work.ExecuteInTransactionAsync(async token =>
    {
        var totals = await executor.ToListAsync(lines.Query(l => l.LedgerEntry.PostedAt != null)
            .GroupBy(l => l.Account.Type).Select(g => new { Account = g.Key, Net = g.Sum(l => l.Debit - l.Credit) }), token);
        decimal Book(AccountType account) => totals.SingleOrDefault(t => t.Account == account)?.Net ?? 0;
        var stock = (await executor.ToListAsync(stocks.Query(_ => true).Select(s => new { Value = s.TotalCost }), token)).Sum(s => s.Value);
        var sales = await executor.ToListAsync(invoices.Query(i => i.Type == InvoiceType.Sales && i.LegacyInvoiceTypeCode == null && i.ParentInvoiceId == null)
            .Select(i => new { i.Id, i.Status, i.RemainingAmount, i.PaidAmount }), token);
        var opening = await executor.ToListAsync(debts.Query(d => d.Kind == DebtItemKind.OpeningBalance).Select(d => new { d.Id, d.OriginalAmount }), token);
        var ids = opening.Select(d => d.Id).ToArray();
        var openingSettlements = (await executor.ToListAsync(lines.Query(l => l.LedgerEntry.PostedAt != null && l.DebtItemId.HasValue && ids.Contains(l.DebtItemId.Value))
            .Select(l => new { Value = l.Credit }), token)).Sum(l => l.Value);
        var refunded = (await executor.ToListAsync(payments.Query(p => p.PaymentType == PaymentType.Payment && p.Purpose == PaymentPurpose.CustomerRefund && p.InvoiceId.HasValue)
            .Select(p => new { Value = p.Amount }), token)).Sum(p => p.Value);
        return (IReadOnlyList<ReconciliationRow>)new ReconciliationRow[] {
            new(AccountType.Inventory, Book(AccountType.Inventory), stock),
            new(AccountType.Receivables, Book(AccountType.Receivables), sales.Where(i => i.Status is InvoiceStatus.Shipped or InvoiceStatus.Delivered).Sum(i => i.RemainingAmount) + opening.Sum(d => d.OriginalAmount) - openingSettlements),
            new(AccountType.CustomerAdvances, -Book(AccountType.CustomerAdvances), sales.Where(i => i.Status is InvoiceStatus.Pending or InvoiceStatus.Confirmed).Sum(i => i.PaidAmount)),
            new(AccountType.CustomerRefunds, -Book(AccountType.CustomerRefunds), sales.Where(i => i.Status == InvoiceStatus.Cancelled).Sum(i => i.PaidAmount) - refunded)
        };
    }, ct);
}
