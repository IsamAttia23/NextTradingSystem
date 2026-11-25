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
    IQueryExecutor executor, IPaginationServices pagination, IEntityValidator<PaginationQuery> validator,
    IReadRepository<Payment> payments, IReadRepository<Customer> customers) : IAccountingReadService
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

    public Task<TPaginationQueryModel<AccountTransactionDto>> GetTransactionsAsync(AccountingQuery query, CancellationToken ct,
        AccountingSearchMatches? translatedSearch = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        validator.Validate(query);
        Dates(query.From, query.To);
        if (query.Account.HasValue && !Enum.IsDefined(query.Account.Value))
            RequestValidation.Fail(nameof(query.Account), "AccountingInvalidReference");
        foreach (var id in new[] { query.CustomerId, query.InvoiceId, query.PaymentId })
            if (id.HasValue) RequestValidation.Identifier(id.Value);
        var accountId = query.Account.HasValue ? Account.IdFor(query.Account.Value) : (Guid?)null;
        var term = query.SearchTerm?.Trim().ToLowerInvariant() ?? "";
        var kinds = translatedSearch?.Kinds ?? [];
        var purposes = translatedSearch?.Purposes ?? [];
        var methods = translatedSearch?.Methods ?? [];
        var lines = movements.Query(line => line.LedgerEntry.PostedAt != null &&
            (!accountId.HasValue || line.AccountId == accountId) &&
            (!query.From.HasValue || line.LedgerEntry.EntryDate >= query.From.Value) &&
            (!query.To.HasValue || line.LedgerEntry.EntryDate <= query.To.Value) &&
            (!query.CustomerId.HasValue || line.CustomerId == query.CustomerId ||
                (line.LedgerEntry.Invoice != null && line.LedgerEntry.Invoice.CustomerId == query.CustomerId)) &&
            (!query.PaymentId.HasValue || line.LedgerEntry.PaymentId == query.PaymentId) &&
            (!query.InvoiceId.HasValue || line.LedgerEntry.InvoiceId == query.InvoiceId ||
                (line.DebtItem != null && line.DebtItem.InvoiceId == query.InvoiceId)));
        var joined = from line in lines
            join payment in payments.Query(_ => true) on line.LedgerEntry.PaymentId equals (Guid?)payment.Id into paymentGroup
            from payment in paymentGroup.DefaultIfEmpty()
            join customer in customers.Query(_ => true) on line.CustomerId equals (Guid?)customer.Id into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            select new { Line = line, Payment = payment, Customer = customer };
        var ordered = joined.Where(row => term == "" || row.Line.LedgerEntry.Description.ToLower().Contains(term) ||
                kinds.Contains(row.Line.LedgerEntry.Kind) ||
                (row.Line.Description != null && row.Line.Description.ToLower().Contains(term)) ||
                (row.Customer != null && row.Customer.Name.ToLower().Contains(term)) ||
                (row.Payment != null && (row.Payment.PaymentNumber.ToLower().Contains(term) ||
                    purposes.Contains(row.Payment.Purpose) || methods.Contains(row.Payment.PaymentMethod))) ||
                (row.Line.LedgerEntry.Invoice != null &&
                    (row.Line.LedgerEntry.Invoice.InvoiceNumber.ToLower().Contains(term) ||
                     (row.Line.LedgerEntry.Invoice.CustomerName != null && row.Line.LedgerEntry.Invoice.CustomerName.ToLower().Contains(term)))))
            .OrderByDescending(row => row.Line.LedgerEntry.EntryDate)
            .ThenByDescending(row => row.Line.LedgerEntry.CreatedAt).ThenBy(row => row.Line.LedgerEntryId).ThenBy(row => row.Line.Sequence);
        return pagination.GetPagedList(ordered, source => source.Select(row => new AccountTransactionDto(row.Line.Id,
            row.Line.LedgerEntryId, row.Line.Account.Type, row.Line.LedgerEntry.EntryDate, row.Line.Description ?? row.Line.LedgerEntry.Description,
            row.Line.Debit, row.Line.Credit, row.Line.CustomerId, row.Line.DebtItem != null ? row.Line.DebtItem.InvoiceId : row.Line.LedgerEntry.InvoiceId,
            row.Line.DebtItem != null && row.Line.DebtItem.Invoice != null ? row.Line.DebtItem.Invoice.InvoiceNumber :
                row.Line.LedgerEntry.Invoice != null ? row.Line.LedgerEntry.Invoice.InvoiceNumber : null,
            row.Line.LedgerEntry.PaymentId, row.Line.LedgerEntry.ActorName,
            row.Payment != null ? row.Payment.PaymentMethod : null, row.Payment != null ? row.Payment.PaymentNumber : null,
            row.Customer != null ? row.Customer.Name : row.Line.LedgerEntry.Invoice != null ? row.Line.LedgerEntry.Invoice.CustomerName : null,
            row.Line.LedgerEntry.Kind, row.Payment != null ? row.Payment.Purpose : null)), query, ct);
    }

    private static void Dates(DateOnly? from, DateOnly? to)
    {
        if (from > to) RequestValidation.Fail("From", "AccountingDateRangeInvalid");
    }

    private sealed record AccountTotals(Guid AccountId, decimal Debit, decimal Credit, decimal Opening);
}
