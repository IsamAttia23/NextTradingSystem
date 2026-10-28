using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NEXT.Domain.Common.Entities;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

public sealed class SoftDeleteHandler(AuditValueSerializer valueSerializer)
{
    public bool TryHandle(
        NEXTDbContext context,
        EntityEntry entry,
        DateTimeOffset now,
        Guid? userId)
    {
        if (entry.State != EntityState.Deleted || context.IsPermanentDeletion(entry.Entity))
            return false;

        var snapshot = valueSerializer.Serialize(entry, originalValues: true);
        string entityType;
        Guid entityId;

        if (entry.Entity is IsDeletableEntity<Guid> softDeletable)
        {
            softDeletable.DeletedAt = now;
            softDeletable.DeletedBy = userId;
            entityId = softDeletable.Id;
            entityType = entry.Metadata.ClrType.Name;
        }
        else if (entry.Entity is ApplicationUser user)
        {
            user.DeletedAt = now;
            user.DeletedBy = userId;
            user.IsActive = false;
            entityId = user.Id;
            entityType = entry.Metadata.ClrType.Name;
        }
        else
        {
            return false;
        }

        // Turn DELETE into UPDATE so the row remains available for restoration.
        entry.State = EntityState.Modified;

        context.RecycleBinEntries.Add(new RecycleBinEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            EntityDataJson = snapshot,
            DeletedBy = userId,
            DeleteReason = (entry.Entity as IIsDeletableEntity)?.DeletionReason,
            DeletedAt = now
        });

        return true;
    }
}
