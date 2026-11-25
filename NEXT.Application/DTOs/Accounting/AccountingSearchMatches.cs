using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Accounting;

public sealed record AccountingSearchMatches(LedgerEntryKind[] Kinds, PaymentPurpose[] Purposes, PaymentMethod[] Methods);
