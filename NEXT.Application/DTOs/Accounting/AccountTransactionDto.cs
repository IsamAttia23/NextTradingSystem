using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Accounting;

public sealed record AccountTransactionDto(Guid Id, Guid EntryId, AccountType Account, DateOnly Date,
    string Description, decimal Debit, decimal Credit, Guid? CustomerId, Guid? InvoiceId,
    string? InvoiceNumber, Guid? PaymentId, string ActorName);
