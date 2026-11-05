using Microsoft.EntityFrameworkCore;
namespace NEXT.Infrastructure.Persistence.SaveChanges;

// Capture scalar state at a boundary, including unchanged entities that inner writes may modify.
internal sealed class SaveChangesStatePreserver(NEXTDbContext context)
{
    public IReadOnlyList<EntryStateSnapshot> Capture() => context.ChangeTracker.Entries()
        .Select(entry => new EntryStateSnapshot(entry.Entity, entry.State, entry.OriginalValues.Clone(), entry.CurrentValues.Clone(),
            entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).ToArray(),
            entry.Properties.Where(property => property.IsTemporary).Select(property => property.Metadata.Name).ToArray())).ToArray();

    public void MarkWritten() => context.ChangeTracker.AcceptAllChanges();
    public void Restore(IReadOnlyList<EntryStateSnapshot> snapshots, bool removeNewEntries = true)
    {
        var existing = snapshots.Select(x => x.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
        var retained = removeNewEntries ? snapshots : snapshots.Concat(Capture().Where(x => !existing.Contains(x.Entity))).ToArray();
        // Detach first so generated keys can be restored without modifying a tracked primary key.
        context.ChangeTracker.Clear();
        foreach (var snapshot in retained)
        {
            var entry = context.Entry(snapshot.Entity);
            entry.CurrentValues.SetValues(snapshot.CurrentValues);
            entry.State = snapshot.State == EntityState.Modified ? EntityState.Unchanged : snapshot.State;
            entry.OriginalValues.SetValues(snapshot.OriginalValues);
            foreach (var property in snapshot.TemporaryProperties)
            {
                entry.Property(property).IsTemporary = true;
            }
            if (snapshot.State == EntityState.Modified)
            {
                foreach (var property in snapshot.ModifiedProperties)
                {
                    entry.Property(property).IsModified = true;
                }
            }
        }
    }
}
