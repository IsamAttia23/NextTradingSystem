using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Invoices;

public sealed record InvoiceStatusEntry(InvoiceStatus FromStatus, InvoiceStatus ToStatus,
    DateTimeOffset ActionAt, string ActorFullName, string? Reason);
