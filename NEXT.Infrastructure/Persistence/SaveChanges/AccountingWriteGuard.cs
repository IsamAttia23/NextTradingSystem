using Microsoft.EntityFrameworkCore;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

internal static class AccountingWriteGuard
{
    public static void Validate(NEXTDbContext db)
    {
        ValidateTrackedEntries(db);
        var payments = ChangedPayments(db);
        if (payments.Length > 0 && (HasPendingReferences(db, payments) || db.LedgerEntries.Any(item => item.PostedAt != null && item.PaymentId.HasValue && payments.Contains(item.PaymentId.Value))))
            throw new ConflictException("AccountingPostedPaymentImmutable");
    }

    public static async Task ValidateAsync(NEXTDbContext db, CancellationToken ct)
    {
        ValidateTrackedEntries(db);
        var payments = ChangedPayments(db);
        if (payments.Length > 0 && (HasPendingReferences(db, payments) || await db.LedgerEntries.AnyAsync(item => item.PostedAt != null && item.PaymentId.HasValue && payments.Contains(item.PaymentId.Value), ct)))
            throw new ConflictException("AccountingPostedPaymentImmutable");
    }

    private static Guid[] ChangedPayments(NEXTDbContext db) => db.ChangeTracker.Entries<Payment>()
        .Where(item => item.State is EntityState.Modified or EntityState.Deleted).Select(item => item.Entity.Id).ToArray();

    private static bool HasPendingReferences(NEXTDbContext db, Guid[] payments) => db.ChangeTracker.Entries<LedgerEntry>()
        .Any(item => item.State == EntityState.Added && item.Entity.PaymentId is { } id && payments.Contains(id));

    private static void ValidateTrackedEntries(NEXTDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        foreach (var entry in db.ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is Account account)
            {
                if (entry.State != EntityState.Added) throw new ConflictException("AccountingFixedAccountsImmutable");
                var expected = Enum.IsDefined(account.Type) ? Account.Create(account.Type) : null;
                if (expected is null || account.Id != expected.Id || account.Code != expected.Code || account.Name != expected.Name ||
                    account.NormalSide != expected.NormalSide || account.IsTechnical != expected.IsTechnical)
                    throw new ConflictException("AccountingFixedAccountsImmutable");
            }
            if (entry.Entity is AccountTransaction line)
            {
                if (entry.State != EntityState.Added) throw new ConflictException("AccountingEntryImmutable");
                var parent = db.ChangeTracker.Entries<LedgerEntry>().SingleOrDefault(item => item.Entity.Id == line.LedgerEntryId);
                if (parent is null || parent.Entity.PostedAt is not null) throw new ConflictException("AccountingEntryImmutable");
            }
            if (entry.Entity is LedgerEntry ledger)
            {
                if (entry.State == EntityState.Added)
                {
                    if (ledger.PostedAt is not null) throw new ConflictException("AccountingEntryImmutable");
                    ledger.Validate();
                }
                else if (entry.State != EntityState.Modified || entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(LedgerEntry.PostedAt)) is not null ||
                    ledger.PostedAt is null || entry.Properties.Any(property => property.IsModified && property.Metadata.Name != nameof(LedgerEntry.PostedAt)))
                    throw new ConflictException("AccountingEntryImmutable");
            }
            if (entry.Entity is DebtItem && entry.State != EntityState.Added)
                throw new ConflictException("AccountingEntryImmutable");
            if (entry.Entity is CustomerDebtAccount && (entry.State == EntityState.Deleted ||
                (entry.State == EntityState.Modified && entry.Properties.Any(property => property.IsModified &&
                    property.Metadata.Name is not (nameof(CustomerDebtAccount.LastActivityAt) or nameof(CustomerDebtAccount.RowVersion))))))
                throw new ConflictException("AccountingDebtAccountImmutable");
        }
    }
}
