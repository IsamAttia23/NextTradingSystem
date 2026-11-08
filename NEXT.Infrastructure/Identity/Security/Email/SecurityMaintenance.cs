using Microsoft.EntityFrameworkCore;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class SecurityMaintenance(NEXTDbContext db, TimeProvider clock)
{
    public async Task CleanupAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-1);
        var expiredMail = await db.SecurityEmailMessages.Where(x => x.ExpiresAt <= now && x.EncryptedBody != "")
            .Take(500).ToListAsync(ct);
        foreach (var row in expiredMail)
        {
            row.EncryptedBody = "";
            row.CancelledAt ??= now;
        }
        db.OtpChallenges.RemoveRange(await db.OtpChallenges.Where(x => x.ExpiresAt < cutoff).Take(500).ToListAsync(ct));
        db.AccountSecurityGrants.RemoveRange(await db.AccountSecurityGrants.Where(x => x.ExpiresAt < cutoff).Take(500).ToListAsync(ct));
        db.SecurityRequestWindows.RemoveRange(await db.SecurityRequestWindows.Where(x => x.LastRequestAt < cutoff).Take(500).ToListAsync(ct));
        db.SecurityEmailMessages.RemoveRange(await db.SecurityEmailMessages.Where(x => x.ExpiresAt < now.AddDays(-7))
            .Take(500).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }
}
