using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NEXT.Domain.Common.Entities;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

public sealed class AuditFieldsHandler
{
    public void Apply(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        if (entry.State == EntityState.Added && entry.Entity is AuditableEntity<Guid> added)
        {
            added.CreatedAt = now;
            added.CreatedBy ??= userId;
            return;
        }

        if (entry.State != EntityState.Modified || entry.Entity is not AuditableEntity<Guid> modified)
            return;

        modified.UpdatedAt = now;
        modified.UpdatedBy = userId;

        entry.Property(nameof(AuditableEntity<Guid>.CreatedAt)).IsModified = false;
        entry.Property(nameof(AuditableEntity<Guid>.CreatedBy)).IsModified = false;
    }
}
