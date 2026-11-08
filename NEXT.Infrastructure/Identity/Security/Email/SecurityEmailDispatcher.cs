using NEXT.Infrastructure.Identity.Security.Storage;
using NEXT.Infrastructure.Identity.Security.Cryptography;
using NEXT.Infrastructure.Identity.Security.Contracts;
using Microsoft.EntityFrameworkCore;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class SecurityEmailDispatcher(NEXTDbContext db, SecurityTransactions transactions,
    SecuritySecrets secrets, IEmailTransport transport, TimeProvider clock)
{
    public async Task<bool> DispatchOneAsync(CancellationToken ct)
    {
        var id = await transactions.RunAsync("email-claim", async () =>
        {
            var now = clock.GetUtcNow();
            var row = await db.SecurityEmailMessages
                .Where(x => x.SentAt == null && x.CancelledAt == null && x.NextAttemptAt <= now &&
                    (x.ClaimedUntil == null || x.ClaimedUntil <= now))
                .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            if (row is null) return (Guid?)null;
            row.ClaimedUntil = now.AddMinutes(2);
            row.Attempts++;
            await db.SaveChangesAsync(ct);
            return row.Id;
        }, ct);
        if (id is null) return false;

        var message = await db.SecurityEmailMessages.FindAsync([id.Value], ct);
        if (message is null) return true;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == message.UserId, ct);
        var valid = user is { IsActive: true, RequiresAdministratorUnlock: false } &&
            message.ExpiresAt > clock.GetUtcNow() && user.SecurityStamp == message.CredentialStamp &&
            user.Email == message.Recipient;
        if (valid && message.OtpChallengeId is { } challengeId)
            valid = await db.OtpChallenges.AsNoTracking().AnyAsync(x => x.Id == challengeId &&
                x.UsedAt == null && x.RevokedAt == null && x.ExpiresAt > clock.GetUtcNow(), ct);
        if (!valid)
        {
            message.CancelledAt = clock.GetUtcNow();
            message.EncryptedBody = "";
        }
        else
        {
            try
            {
                await transport.SendAsync(message.Recipient, message.Subject,
                    secrets.Decrypt(message.EncryptedBody), ct);
                message.SentAt = clock.GetUtcNow();
                message.EncryptedBody = "";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                // Delivery details can contain credentials; persist only retry state.
                message.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Min(300, 15 * message.Attempts));
                if (message.Attempts >= 5)
                {
                    message.CancelledAt = clock.GetUtcNow();
                    message.EncryptedBody = "";
                }
            }
        }
        message.ClaimedUntil = null;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
