using Microsoft.EntityFrameworkCore;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

internal static class AccountingWriteGuard
{
    public static void Validate(NEXTDbContext db)
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
