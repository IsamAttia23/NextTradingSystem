using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Accounting;

public sealed record PaymentPostingState(Guid? InvoiceId, Guid? CustomerId, decimal Amount,
    PaymentType Type, DateOnly Date, PaymentPurpose Purpose = PaymentPurpose.Unspecified, AccountType CashAccount = AccountType.Treasury);
