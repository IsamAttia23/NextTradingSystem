using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.QueryModels;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Accounting;

public sealed class AccountingReadService(IReadRepository<Account> accounts, IReadRepository<AccountTransaction> movements,
    IQueryExecutor executor, IPaginationServices pagination, IEntityValidator<PaginationQuery> validator) : IAccountingReadService
{
    public async Task<IReadOnlyList<AccountSummaryDto>> GetAccountsAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        Dates(from, to);
        var catalog = await executor.ToListAsync(accounts.Query(_ => true).OrderBy(item => item.Type), ct);
        var totals = await executor.ToListAsync(movements.Query(line => line.LedgerEntry.PostedAt != null &&
                (!to.HasValue || line.LedgerEntry.EntryDate <= to.Value))
            .GroupBy(line => line.AccountId).Select(group => new AccountTotals(group.Key,
                group.Sum(line => !from.HasValue || line.LedgerEntry.EntryDate >= from.Value ? line.Debit : 0),
                group.Sum(line => !from.HasValue || line.LedgerEntry.EntryDate >= from.Value ? line.Credit : 0),
                group.Sum(line => from.HasValue && line.LedgerEntry.EntryDate < from.Value ? line.Debit - line.Credit : 0))), ct);
        return catalog.Select(account =>
        {
            var total = totals.SingleOrDefault(item => item.AccountId == account.Id) ?? new(account.Id, 0, 0, 0);
            var sign = account.NormalSide == DebitCreditSide.Debit ? 1 : -1;
            return new AccountSummaryDto(account.Id, account.Type, account.Name, account.IsTechnical,
                sign * total.Opening, total.Debit, total.Credit, sign * (total.Opening + total.Debit - total.Credit));
        }).ToArray();
    }

    public Task<TPaginationQueryModel<AccountTransactionDto>> GetTransactionsAsync(AccountingQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        validator.Validate(query);
        Dates(query.From, query.To);
        if (query.Account.HasValue && !Enum.IsDefined(query.Account.Value))
            RequestValidation.Fail(nameof(query.Account), "AccountingInvalidReference");
        foreach (var id in new[] { query.CustomerId, query.InvoiceId, query.PaymentId })
            if (id.HasValue) RequestValidation.Identifier(id.Value);
        var accountId = query.Account.HasValue ? Account.IdFor(query.Account.Value) : (Guid?)null;
        var term = query.SearchTerm?.Trim() ?? "";
        var lines = movements.Query(line => line.LedgerEntry.PostedAt != null &&
            (!accountId.HasValue || line.AccountId == accountId) &&
            (!query.From.HasValue || line.LedgerEntry.EntryDate >= query.From.Value) &&
            (!query.To.HasValue || line.LedgerEntry.EntryDate <= query.To.Value) &&
            (!query.CustomerId.HasValue || line.CustomerId == query.CustomerId) &&
            (!query.PaymentId.HasValue || line.LedgerEntry.PaymentId == query.PaymentId) &&
            (!query.InvoiceId.HasValue || line.LedgerEntry.InvoiceId == query.InvoiceId ||
                (line.DebtItem != null && line.DebtItem.InvoiceId == query.InvoiceId)) &&
            (term == "" || line.LedgerEntry.Description.Contains(term) || (line.Description != null && line.Description.Contains(term))))
            .OrderByDescending(line => line.LedgerEntry.EntryDate).ThenByDescending(line => line.LedgerEntry.CreatedAt)
            .ThenBy(line => line.LedgerEntryId).ThenBy(line => line.Sequence);
        return pagination.GetPagedList(lines, source => source.Select(line => new AccountTransactionDto(line.Id,
            line.LedgerEntryId, line.Account.Type, line.LedgerEntry.EntryDate, line.Description ?? line.LedgerEntry.Description,
            line.Debit, line.Credit, line.CustomerId, line.DebtItem != null ? line.DebtItem.InvoiceId : line.LedgerEntry.InvoiceId,
            line.DebtItem != null && line.DebtItem.Invoice != null ? line.DebtItem.Invoice.InvoiceNumber :
                line.LedgerEntry.Invoice != null ? line.LedgerEntry.Invoice.InvoiceNumber : null,
            line.LedgerEntry.PaymentId, line.LedgerEntry.ActorName)), query, ct);
    }

    private static void Dates(DateOnly? from, DateOnly? to)
    {
        if (from > to) RequestValidation.Fail("From", "AccountingDateRangeInvalid");
    }

    private sealed record AccountTotals(Guid AccountId, decimal Debit, decimal Credit, decimal Opening);
}
