namespace NEXT.Application.DTOs.Presentation;

public sealed record RelatedRecordDto(Guid Id, IReadOnlyDictionary<string, object?> Values)
{
    // Columns allowed by this read contract; credentials, binary data and navigation graphs are excluded.
    public static IReadOnlySet<string> FieldNames { get; } = new HashSet<string>
    {
        "Name", "FullName", "UserName", "Code", "Number", "InvoiceNumber", "PaymentNumber", "BatchNumber",
        "InvoiceDate", "OrderDate", "PaymentDate", "ExpiryDate", "CreatedAt", "Type", "PaymentType", "Status", "PaymentStatus",
        "Quantity", "ReservedQuantity", "AverageCost", "UnitPrice", "UnitCost", "CostPrice", "TotalAmount", "Amount", "PaidAmount", "RemainingAmount",
        "Phone", "Email", "Position", "Description", "Notes", "IsActive"
    };
}
