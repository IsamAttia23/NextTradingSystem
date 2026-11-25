using NEXT.Application.DTOs.Invoices;

namespace NEXT.web.Models;

public sealed record InvoiceDraftForm(InvoiceDraftInput Input, string Action = "Create", Guid? Id = null,
    byte[]? RowVersion = null, IReadOnlyList<InvoiceProductOption>? Products = null);
