using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Accounting;

public sealed record InvoicePostingState(Guid Id, InvoiceType Type, InvoiceStatus Status, decimal Total,
    decimal Paid, decimal Remaining, Guid? CustomerId, decimal Tax, int? LegacyType, Guid? ParentInvoiceId, DateTimeOffset Date);
