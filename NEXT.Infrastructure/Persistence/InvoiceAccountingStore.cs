using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence;

public sealed class InvoiceAccountingStore(NEXTDbContext db) : IInvoiceAccountingStore
{
    public async Task LockCustomerNameAsync(string normalizedName, CancellationToken ct)
    {
        RequireTransaction();
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"accounting:customer-name:" + normalizedName}, 0))", ct);
    }

    public async Task<Customer?> LockCustomerAsync(Guid id, CancellationToken ct)
    {
        RequireTransaction();
        var query = db.Database.IsNpgsql()
            ? db.Customers.FromSqlInterpolated($"SELECT * FROM \"Customers\" WHERE \"Id\" = {id} FOR UPDATE")
            : db.Customers.Where(item => item.Id == id);
        return await query.AsTracking().SingleOrDefaultAsync(ct);
    }

    public async Task<CustomerDebtAccount?> LockDebtAccountAsync(Guid customerId, CancellationToken ct)
    {
        RequireTransaction();
        var query = db.Database.IsNpgsql()
            ? db.CustomerDebtAccounts.FromSqlInterpolated($"SELECT * FROM \"CustomerDebtAccounts\" WHERE \"CustomerId\" = {customerId} FOR UPDATE")
            : db.CustomerDebtAccounts.Where(item => item.CustomerId == customerId);
        return await query.AsTracking().SingleOrDefaultAsync(ct);
    }

    private void RequireTransaction()
    {
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Invoice accounting requires the invoice transaction.");
    }
}
