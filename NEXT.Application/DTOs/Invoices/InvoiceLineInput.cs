namespace NEXT.Application.DTOs.Invoices;

public sealed class InvoiceLineInput
{
    public Guid? ProductId { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? QuotedUnitPrice { get; set; }
}
