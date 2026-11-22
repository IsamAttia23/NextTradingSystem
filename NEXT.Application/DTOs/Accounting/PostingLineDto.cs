using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Accounting;

public sealed record PostingLineDto(AccountType Account, decimal Debit, decimal Credit,
    Guid? CustomerId = null, Guid? DebtItemId = null, string? Description = null);
