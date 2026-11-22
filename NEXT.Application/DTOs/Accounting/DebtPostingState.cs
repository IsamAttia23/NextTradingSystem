using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Accounting;

public sealed record DebtPostingState(Guid Id, Guid CustomerId, bool CustomerActive, string? Phone,
    DebtItemKind Kind, decimal OriginalAmount, decimal Balance);
