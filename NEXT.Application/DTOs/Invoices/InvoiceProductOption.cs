namespace NEXT.Application.DTOs.Invoices;

public sealed record InvoiceProductOption(Guid Id, string Name, string? Barcode,
    decimal UnitPrice, decimal UnitCost, decimal AvailableQuantity);
