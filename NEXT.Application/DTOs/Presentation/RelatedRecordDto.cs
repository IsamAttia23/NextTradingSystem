namespace NEXT.Application.DTOs.Presentation;
public sealed record RelatedRecordDto(Guid Id, IReadOnlyDictionary<string, object?> Values)
{
    // Columns allowed by this read contract; credentials, binary data and navigation graphs are excluded.
    public static IReadOnlySet<string> FieldNames { get; } = new HashSet<string>
    {
        "Name", "FullName", "UserName", "Code", "Number", "InvoiceNumber", "PaymentNumber", "EntryNumber", "BatchNumber",
        "InvoiceDate", "OrderDate", "PaymentDate", "EntryDate", "ExpiryDate", "CreatedAt", "Type", "InvoiceType", "PaymentType", "Status", "PaymentStatus",
        "Quantity", "ReservedQuantity", "AverageCost", "UnitPrice", "UnitCost", "CostPrice", "TotalAmount", "Amount", "PaidAmount", "RemainingAmount",
        "TotalDebit", "TotalCredit", "Debit", "Credit", "Phone", "Email", "Position", "Description", "Notes", "IsActive"
    };
}
