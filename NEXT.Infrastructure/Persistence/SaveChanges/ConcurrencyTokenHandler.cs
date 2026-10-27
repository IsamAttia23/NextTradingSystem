using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

// PostgreSQL bytea has no SQL Server-style automatic rowversion generation.
public static class ConcurrencyTokenHandler
{
    public static void Apply(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified))
            return;
        var property = entry.Metadata.FindProperty("RowVersion");
        if (property is null || property.ClrType != typeof(byte[]) || !property.IsConcurrencyToken)
            return;
        // Keep OriginalValue for the UPDATE predicate; only the new token is replaced.
        entry.Property(property.Name).CurrentValue = Guid.NewGuid().ToByteArray();
    }
}
