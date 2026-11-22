using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.QueryModels;

namespace NEXT.Application.Abstractions.Services;

public interface IAccountingReadService
{
    Task<IReadOnlyList<AccountSummaryDto>> GetAccountsAsync(DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<TPaginationQueryModel<AccountTransactionDto>> GetTransactionsAsync(AccountingQuery query, CancellationToken ct);
}
