using MediatR;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Invoices.Events;

// Synchronous notification, published inside the invoice's unit of work.
public sealed record InvoicePaymentRecorded(Invoice Invoice, Payment Payment) : INotification;
