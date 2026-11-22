namespace NEXT.Application.DTOs.Invoices;

public sealed record InvoiceDetails(InvoiceSummary Summary, string? CreatedByFullName, decimal SubTotal,
    decimal DiscountAmount, decimal PaidAmount, decimal RemainingAmount, IReadOnlyList<InvoiceLineDetails> Lines)
{
    public byte[] RowVersion { get; init; } = [];
    public bool CanConfirm { get; init; }
    public bool CanRecordPayment { get; init; }
    public IReadOnlyList<NEXT.Domain.Enums.InvoiceStatus> FulfillmentTargets { get; init; } = [];
    public IReadOnlyList<InvoicePaymentEntry> Payments { get; init; } = [];
    public DateTimeOffset? LastActionAt { get; init; }
    public IReadOnlyList<InvoiceStatusEntry> History { get; init; } = [];
    public Guid? CustomerId { get; init; }
    public string? Notes { get; init; }
}
