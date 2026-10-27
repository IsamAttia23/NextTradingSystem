using NEXT.Application.Abstractions.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Services;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

/// <summary>Prepares tracked entities for saving and captures the original audit values.</summary>
public sealed class TrackedChangesProcessor(
    ICurrentUserService currentUserService,
    AuditValueSerializer valueSerializer,
    SoftDeleteHandler softDeleteHandler,
    AuditFieldsHandler auditFieldsHandler)
{
    internal IReadOnlyList<PendingAuditEntry> Prepare(NEXTDbContext context)
    {
        context.ChangeTracker.DetectChanges();

        var now = DateTimeOffset.UtcNow;
        var userId = currentUserService.UserId;
        var pending = new List<PendingAuditEntry>();

        // Materialize before changing entity states inside the loop.
        var entries = context.ChangeTracker.Entries()
            .Where(IsPotentiallyChanged)
            .ToList();

        foreach (var entry in entries)
        {
            if (ShouldSkip(entry))
                continue;

            var wasSoftDeleted = softDeleteHandler.TryHandle(context, entry, now, userId);
            auditFieldsHandler.Apply(entry, now, userId);
            ConcurrencyTokenHandler.Apply(entry);

            var action = GetAction(entry, wasSoftDeleted);
            if (action is null)
                continue;

            var oldValues = action == AuditAction.Create
                ? null
                : valueSerializer.Serialize(entry, originalValues: true);

            pending.Add(new PendingAuditEntry(entry, action.Value, oldValues));
        }

        return pending;
    }

    private static bool IsPotentiallyChanged(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    private static bool ShouldSkip(EntityEntry entry) =>
        entry.Entity is AuditLog or RecycleBinEntry or UserRole or
            IdentityUserToken<Guid> or IdentityUserLogin<Guid> or
            IdentityUserClaim<Guid> or IdentityRoleClaim<Guid> or
            IdentityUserRole<Guid> ||
        entry.Metadata.IsOwned();

    private static AuditAction? GetAction(EntityEntry entry, bool wasSoftDeleted)
    {
        if (wasSoftDeleted)
            return AuditAction.Delete;

        return entry.State switch
        {
            EntityState.Added => AuditAction.Create,
            EntityState.Modified when IsDeletedNow(entry) => AuditAction.Delete,
            EntityState.Modified when IsRestored(entry) => AuditAction.Restore,
            EntityState.Modified => AuditAction.Update,
            EntityState.Deleted => AuditAction.Purge,
            _ => null
        };
    }

    private static bool IsDeletedNow(EntityEntry entry)
    {
        var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAt");
        return property?.OriginalValue is null && property?.CurrentValue is DateTimeOffset;
    }

    private static bool IsRestored(EntityEntry entry)
    {
        var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAt");
        return property?.OriginalValue is DateTimeOffset && property?.CurrentValue is null;
    }
}
