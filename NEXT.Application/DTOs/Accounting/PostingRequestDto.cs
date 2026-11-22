using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Accounting;

// Internal application contract, not an HTTP form for unrestricted journal input.
public sealed record PostingRequestDto(Guid OperationId, LedgerEntryKind Kind,
    AccountingReferenceType ReferenceType, Guid ReferenceId, DateOnly Date, string Description,
    IReadOnlyList<PostingLineDto> Lines, Guid? InvoiceId = null, Guid? PaymentId = null, Guid? ReversesEntryId = null);
