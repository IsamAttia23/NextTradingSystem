using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Invoices;

public sealed record InvoiceSummary(Guid Id, string Number, DateTimeOffset Date, string? CustomerName,
    decimal Total, InvoiceStatus Status, PaymentStatus PaymentStatus);
