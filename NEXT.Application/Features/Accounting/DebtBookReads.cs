using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.QueryModels;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
namespace NEXT.Application.Features.Accounting;

public sealed class DebtBookReads(IReadRepository<CustomerDebtAccount> accounts, IReadRepository<AccountTransaction> lines,
 IReadRepository<Invoice> invoices, IReadRepository<InvoiceItem> items, IReadRepository<Payment> payments,
 IReadRepository<LedgerEntry> entries, IReadRepository<DebtItem> debts, IReadRepository<DebtAccountPayment> receipts,
 IQueryExecutor executor, IPaginationServices pagination, IUnitOfWork work, NEXT.Application.Validation.IEntityValidator<PaginationQuery> validator)
{
    public Task<TPaginationQueryModel<DebtBookSummary>> ListAsync(DebtBookQuery query, CancellationToken ct)
    {
        validator.Validate(query);
        var receivable = Account.IdFor(AccountType.Receivables);
        var posted = lines.Query(t => t.AccountId == receivable && t.LedgerEntry.PostedAt != null);
        var rows = accounts.Query(_ => true).Select(a => new DebtBookSummary
        {
            Id = a.Id,
            CustomerId = a.CustomerId,
            Name = a.Customer.Name,
            Phone = a.Customer.Phone,
            LastActivity = a.LastActivityAt,
            CycleNumber = a.CycleNumber, ClosedAt = a.ClosedAt,
            Balance = posted.Where(t => t.DebtItem != null && t.DebtItem.DebtAccountId == a.Id).Sum(t => (decimal?)(t.Debit - t.Credit)) ?? 0
        });
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        { var term = query.SearchTerm.Trim().ToUpper(); rows = rows.Where(a => a.Name.ToUpper().Contains(term) || (a.Phone != null && a.Phone.ToUpper().Contains(term))); }
        if(query.IsClosed.HasValue) rows = rows.Where(a => a.ClosedAt.HasValue == query.IsClosed.Value);
        if (query.HasBalance.HasValue)
            rows = query.HasBalance.Value ? rows.Where(a => a.Balance > 0) : rows.Where(a => a.Balance == 0);
        rows = (query.SortBy?.ToLowerInvariant(), query.SortDirection == SortDirection.Descending) switch
        {
            ("balance", true) => rows.OrderByDescending(a => a.Balance).ThenBy(a => a.Id),
            ("balance", false) => rows.OrderBy(a => a.Balance).ThenBy(a => a.Id),
            ("lastactivity", true) => rows.OrderByDescending(a => a.LastActivity).ThenBy(a => a.Id),
            ("lastactivity", false) => rows.OrderBy(a => a.LastActivity).ThenBy(a => a.Id),
            (_, true) => rows.OrderByDescending(a => a.Name).ThenBy(a => a.Id),
            _ => rows.OrderBy(a => a.Name).ThenBy(a => a.Id)
        };
        return pagination.GetPagedList(rows, query, ct);
    }
    public async Task<Guid> AccountForInvoiceAsync(Guid invoiceId, CancellationToken ct) =>
        (await executor.FirstOrDefaultAsync(invoices.Query(i => i.Id == invoiceId).Select(i => new { i.DebtAccountId }), ct))?.DebtAccountId
        ?? throw new NotFoundException("DebtBookAccountNotFound");

    public async Task<DebtBookAccount> AccountAsync(Guid id, CancellationToken ct) => await executor.FirstOrDefaultAsync(accounts.Query(a => a.Id == id)
     .Select(a => new DebtBookAccount(a.Id, a.CustomerId, a.Customer.Name, a.Customer.Phone, a.Customer.IsActive, a.RowVersion, a.CycleNumber, a.ClosedAt)), ct) ?? throw new NotFoundException("DebtBookAccountNotFound");
    public Task<DebtBookStatement> StatementAsync(Guid id, CancellationToken ct) => work.ExecuteInTransactionAsync(async token =>
    {
        var a = await AccountAsync(id, token);
        var sales = await executor.ToListAsync(invoices.Query(i => i.DebtAccountId == id && i.Type == InvoiceType.Sales && i.LegacyInvoiceTypeCode == null && i.ParentInvoiceId == null &&
      (i.Status == InvoiceStatus.Shipped || i.Status == InvoiceStatus.Delivered)).OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id)
      .Select(i => new DebtBookInvoice(i.Id, i.InvoiceNumber, i.InvoiceDate, i.TotalAmount, i.DiscountAmount, i.PaidAmount, i.RemainingAmount, Array.Empty<DebtBookLine>())), token);
        var ids = sales.Select(i => i.Id).ToArray();
        var goods = await executor.ToListAsync(items.Query(i => ids.Contains(i.InvoiceId)).OrderBy(i => i.Id)
      .Select(i => new DebtBookLine(i.InvoiceId, i.ProductId.HasValue ? i.Product!.Name : i.ManualName!, i.Quantity, i.UnitPrice, i.TotalAmount)), token);
        var posted = entries.Query(e => e.PostedAt != null && e.PaymentId.HasValue && e.Kind == LedgerEntryKind.Receipt);
        var accountReceiptRows = await executor.ToListAsync(receipts.Query(d => d.DebtAccountId == id).Select(d => new { d.Id }), token);
        var accountReceiptIds = accountReceiptRows.Select(d => d.Id).ToArray();
        var paid = await executor.ToListAsync(payments.Query(p => p.CustomerId == a.CustomerId && p.PaymentType == PaymentType.Receipt &&
      ((p.InvoiceId.HasValue && ids.Contains(p.InvoiceId.Value)) || (!p.InvoiceId.HasValue && p.Purpose == PaymentPurpose.DebtReceipt && p.DebtAccountPaymentId.HasValue && accountReceiptIds.Contains(p.DebtAccountPaymentId.Value))) && posted.Any(e => e.PaymentId == p.Id))
      .OrderBy(p => p.PaymentDate).ThenBy(p => p.CreatedAt).Select(p => new DebtBookReceipt(p.Id, p.PaymentDate, p.PaymentNumber, p.Amount, p.PaymentMethod, p.CashAccount, p.InvoiceId, p.Invoice == null ? null : p.Invoice.InvoiceNumber, p.DebtAccountPaymentId)), token);
        var opening = await executor.ToListAsync(debts.Query(d => d.DebtAccountId == id && d.Kind == DebtItemKind.OpeningBalance).OrderBy(d => d.EffectiveDate)
      .Select(d => new DebtBookOpening(d.EffectiveDate, d.Description, d.OriginalAmount)), token);
        var totals = await executor.ToListAsync(lines.Query(t => t.DebtItem != null && t.DebtItem.DebtAccountId == id && t.AccountId == Account.IdFor(AccountType.Receivables) && t.LedgerEntry.PostedAt != null)
      .Select(t => new { Net = t.Debit - t.Credit }), token);
        var documents = await executor.ToListAsync(receipts.Query(d => d.DebtAccountId == id), token);
        var numbers = documents.ToDictionary(d => d.Id, d => d.Number);
        var descriptions = documents.ToDictionary(d => d.Id, d => d.Notes);
        var result = paid.GroupBy(p => p.AccountPaymentId ?? p.Id).Select(g => g.First() with { Id = g.Key, Number = g.First().AccountPaymentId is { } pid ? numbers[pid] : g.First().Number, Description = g.First().AccountPaymentId is { } receiptId ? descriptions[receiptId] : null, Amount = g.Sum(p => p.Amount), InvoiceId = g.Count() == 1 ? g.First().InvoiceId : null, InvoiceNumber = g.Count() == 1 ? g.First().InvoiceNumber : null }).ToArray();
        return new DebtBookStatement(a, sales.Select(i => i with { Lines = goods.Where(g => g.InvoiceId == i.Id).ToArray() }).ToArray(), result, opening, totals.Sum(t => t.Net));
    }, ct);
}
