using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Invoices;

public sealed record InvoicePaymentEntry(string Number, decimal Amount, PaymentMethod Method,
    DateOnly Date, DateTimeOffset RecordedAt, string? RecordedByFullName);
