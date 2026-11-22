using MediatR;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Invoices.Events;

public sealed record InvoiceStatusChanged(Invoice Invoice, InvoiceStatus From, InvoiceStatus To,
    Guid OperationId, Guid ActorId, Guid? CustomerId, string? CustomerPhone, DateOnly Date) : INotification;
