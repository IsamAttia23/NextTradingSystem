using NEXT.Application.DTOs.Accounting;

namespace NEXT.web.Models;

public sealed record FinancialAccountsView(IReadOnlyList<AccountSummaryDto> Accounts, DateOnly? From, DateOnly? To);
