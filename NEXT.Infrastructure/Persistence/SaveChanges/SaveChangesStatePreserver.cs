using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

/// <summary>
/// Prevents already-written entity changes from being sent to the database a second time
/// when the caller uses SaveChanges(false) and audit rows require a second save.
/// </summary>
internal sealed class SaveChangesStatePreserver(DbContext context)
{
    public IReadOnlyList<EntryStateSnapshot> Capture() => context.ChangeTracker.Entries()
        .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
        .Select(entry => new EntryStateSnapshot(
            entry.Entity,
            entry.State,
            entry.OriginalValues.Clone(),
            entry.Properties.Where(property => property.IsModified)
                .Select(property => property.Metadata.Name)
                .ToArray()))
        .ToList();

    public void MarkUnchanged(IReadOnlyList<EntryStateSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var entry = context.Entry(snapshot.Entity);
            if (entry.State != EntityState.Detached)
                entry.State = EntityState.Unchanged;
        }
    }

    public void Restore(IReadOnlyList<EntryStateSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var entry = context.Entry(snapshot.Entity);
            if (entry.State == EntityState.Detached)
                context.Attach(snapshot.Entity);

            entry = context.Entry(snapshot.Entity);
            entry.State = EntityState.Unchanged;
            entry.OriginalValues.SetValues(snapshot.OriginalValues);

            switch (snapshot.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Added;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Deleted;
                    break;
                case EntityState.Modified:
                    if (snapshot.ModifiedProperties.Count == 0)
                    {
                        entry.State = EntityState.Modified;
                        break;
                    }

                    foreach (var propertyName in snapshot.ModifiedProperties)
                        entry.Property(propertyName).IsModified = true;
                    break;
            }
        }
    }
}

internal sealed record EntryStateSnapshot(
    object Entity,
    EntityState State,
    PropertyValues OriginalValues,
    IReadOnlyList<string> ModifiedProperties);
