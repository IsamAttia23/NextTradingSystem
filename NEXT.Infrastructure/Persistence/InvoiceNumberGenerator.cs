using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Exceptions;

namespace NEXT.Infrastructure.Persistence;

public sealed class InvoiceNumberGenerator(NEXTDbContext db) : IInvoiceNumberGenerator
{
    private const int InvoiceLockNamespace = 0x494E56;

    public async Task<string> NextAsync(DateOnly date, CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
        {
            if (db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Invoice numbers must be allocated inside a write transaction.");

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({InvoiceLockNamespace}, {date.DayNumber})", ct);
        }

        var prefix = "INV-" + date.ToString("MMddyyyy", CultureInfo.InvariantCulture) + "-";
        var numbers = await db.Invoices.Where(invoice => invoice.InvoiceNumber.StartsWith(prefix))
            .Select(invoice => invoice.InvoiceNumber).ToListAsync(ct);
        var maximum = 0L;
        foreach (var number in numbers)
        {
            if (long.TryParse(number.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var serial))
                maximum = Math.Max(maximum, serial);
        }

        if (maximum == long.MaxValue)
            throw new ConflictException("The invoice sequence for this date is exhausted.");
        return prefix + (maximum + 1).ToString("D2", CultureInfo.InvariantCulture);
    }
}
