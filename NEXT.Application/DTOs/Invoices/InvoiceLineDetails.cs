namespace NEXT.Application.DTOs.Invoices;

public sealed record InvoiceLineDetails(string? ProductName, decimal Quantity, decimal UnitPrice, decimal Total);
