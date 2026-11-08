using NEXT.Infrastructure.Identity.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity.Security.Storage;

public sealed class SecurityRequestLimiter(NEXTDbContext db, SecurityTransactions transactions,
    SecuritySecrets secrets, TimeProvider clock)
{
    public Task<bool> AllowAsync(string subject, int limit, TimeSpan window, TimeSpan cooldown,
        CancellationToken ct) => transactions.RunAsync("rate:" + SecuritySecrets.Hash(subject), async () =>
    {
        var key = secrets.Fingerprint(subject);
        var now = clock.GetUtcNow();
        var row = await db.SecurityRequestWindows.FindAsync([key], ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        if (row is null)
        {
            row = new SecurityRequestWindow { Key = key, StartsAt = now };
            db.SecurityRequestWindows.Add(row);
        }
        else if (now - row.StartsAt >= window)
        {
            row.Count = 0;
            row.StartsAt = now;
        }
        if (row.Count >= limit || (row.Count > 0 && now - row.LastRequestAt < cooldown))
            return false;
        row.Count++;
        row.LastRequestAt = now;
        await db.SaveChangesAsync(ct);
        return true;
    }, ct);
}
