namespace NEXT.Application.DTOs.Invoices;

public sealed class EditInvoiceInput : InvoiceDraftInput
{
    public byte[] RowVersion { get; set; } = [];
}
