namespace NEXT.Application.DTOs.Invoices;

public sealed record InvoiceEditDetails(EditInvoiceInput Input, IReadOnlyList<InvoiceProductOption> Products);
