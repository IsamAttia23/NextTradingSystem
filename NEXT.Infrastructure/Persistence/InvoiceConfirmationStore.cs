using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence;

public sealed class InvoiceConfirmationStore(NEXTDbContext db) : IInvoiceConfirmationStore
{
    public async Task<Invoice?> LockInvoiceAsync(Guid id, CancellationToken ct)
    {
        RequireTransaction();
        var query = db.Database.IsNpgsql()
            ? db.Invoices.FromSqlInterpolated($"SELECT * FROM \"Invoices\" WHERE \"Id\" = {id} FOR UPDATE")
            : db.Invoices.Where(invoice => invoice.Id == id);
        var invoice = await query.AsTracking().SingleOrDefaultAsync(ct);
        if (invoice is not null)
            await db.Entry(invoice).Collection(item => item.Items).LoadAsync(ct);
        return invoice;
    }

    public async Task EnsureStocksAsync(Guid warehouseId, Guid[] productIds, CancellationToken ct)
    {
        RequireTransaction();
        foreach (var productId in productIds.Order())
        {
            if (db.Database.IsNpgsql())
            {
                var id = Guid.NewGuid(); var version = Guid.NewGuid().ToByteArray();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Stocks" ("Id", "WarehouseId", "ProductId", "Quantity", "ReservedQuantity", "AverageCost", "TotalCost", "LastUpdated", "CreatedAt", "RowVersion")
                    VALUES ({id}, {warehouseId}, {productId}, 0, 0, 0, 0, now(), now(), {version})
                    ON CONFLICT ("ProductId", "WarehouseId") DO NOTHING
                    """, ct);
            }
            else if (!await db.Stocks.AnyAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId, ct))
            {
                db.Stocks.Add(new Stock { Id = Guid.NewGuid(), ProductId = productId, WarehouseId = warehouseId });
                await db.SaveChangesAsync(ct);
            }
        }
    }

    public async Task<IReadOnlyList<Stock>> LockStocksAsync(Guid warehouseId, Guid[] productIds, CancellationToken ct)
    {
        RequireTransaction();
        var query = db.Database.IsNpgsql()
            ? db.Stocks.FromSqlInterpolated($"""
                SELECT * FROM "Stocks" WHERE "WarehouseId" = {warehouseId} AND "ProductId" = ANY ({productIds})
                ORDER BY "ProductId" FOR UPDATE
                """)
            : db.Stocks.Where(stock => stock.WarehouseId == warehouseId && productIds.Contains(stock.ProductId))
                .OrderBy(stock => stock.ProductId);
        return await query.AsTracking().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductBatch>> LockBatchesAsync(Guid warehouseId, Guid[] productIds, DateOnly today, CancellationToken ct)
    {
        RequireTransaction();
        var query = db.Database.IsNpgsql()
            ? db.Batches.FromSqlInterpolated($"""
                SELECT * FROM "ProductBatches" WHERE "WarehouseId" = {warehouseId} AND "ProductId" = ANY ({productIds})
                AND "Quantity" > 0 AND "ExpiryDate" >= {today}
                ORDER BY "ProductId", "ExpiryDate", "Id" FOR UPDATE
                """)
            : db.Batches.Where(batch => batch.WarehouseId == warehouseId && productIds.Contains(batch.ProductId) &&
                batch.Quantity > 0 && batch.ExpiryDate >= today)
                .OrderBy(batch => batch.ProductId).ThenBy(batch => batch.ExpiryDate).ThenBy(batch => batch.Id);
        return await query.AsTracking().ToListAsync(ct);
    }

    private void RequireTransaction()
    {
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Invoice operation locks require a transaction.");
    }
}
