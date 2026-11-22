namespace NEXT.Domain.Enums;

public enum InvoiceStatus
{
    Pending = 0,
    Draft = 1,
    Confirmed = 2,
    Cancelled = 3,
    Paid = 4,
    PartiallyPaid = 5,
    Shipped = 6,
    Delivered = 7,
}
