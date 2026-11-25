using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Invoices;

public sealed record InvoiceDraft(Guid? CustomerId, string CustomerName, string? Notes, List<InvoiceItem> Items,
    decimal Subtotal, decimal Discount, decimal Total);
