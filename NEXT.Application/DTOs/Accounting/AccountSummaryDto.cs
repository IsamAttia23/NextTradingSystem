using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Accounting;

public sealed record AccountSummaryDto(Guid Id, AccountType Type, string Name, bool IsTechnical,
    decimal OpeningBalance, decimal Debit, decimal Credit, decimal Balance);
