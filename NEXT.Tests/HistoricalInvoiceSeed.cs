using Microsoft.EntityFrameworkCore;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Tests;

internal static class HistoricalInvoiceSeed
{
    // Seed only columns available before the later invoice-source migrations.
    public static async Task InsertAsync(NEXTDbContext db, Invoice invoice)
    {
        if (invoice.Id == Guid.Empty) invoice.Id = Guid.NewGuid();
        invoice.RowVersion = Guid.NewGuid().ToByteArray();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Invoices" ("Id", "InvoiceNumber", "CustomerId", "CustomerName", "WarehouseId", "UserId",
                "InvoiceDate", "Type", "SubTotal", "DiscountValue", "DiscountAmount", "TaxAmount", "TotalAmount",
                "PaidAmount", "RemainingAmount", "ChangeAmount", "PaymentStatus", "Status", "Notes", "RowVersion", "CreatedAt")
            VALUES ({invoice.Id}, {invoice.InvoiceNumber}, {invoice.CustomerId}, {invoice.CustomerName}, {invoice.WarehouseId}, {invoice.UserId},
                {invoice.InvoiceDate}, {(int)invoice.Type}, {invoice.SubTotal}, {invoice.DiscountValue}, {invoice.DiscountAmount}, {invoice.TaxAmount}, {invoice.TotalAmount},
                {invoice.PaidAmount}, {invoice.RemainingAmount}, {invoice.ChangeAmount}, {(int)invoice.PaymentStatus}, {(int)invoice.Status}, {invoice.Notes}, {invoice.RowVersion}, {now});
            """);
    }
}
