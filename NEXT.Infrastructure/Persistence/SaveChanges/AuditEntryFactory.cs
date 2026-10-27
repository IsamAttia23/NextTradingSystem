using NEXT.Application.Abstractions.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Services;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

/// <summary>Creates audit rows after the first save, when database-generated IDs are available.</summary>
public sealed class AuditEntryFactory(
    ICurrentUserService currentUserService,
    IRequestMetadataService requestMetadataService,
    AuditValueSerializer valueSerializer)
{
    internal int AddAuditRows(NEXTDbContext context, IEnumerable<PendingAuditEntry> pendingEntries)
    {
        var count = 0;

        foreach (var pending in pendingEntries)
        {
            context.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserService.UserId,
                Action = pending.Action,
                EntityType = pending.Entry.Metadata.ClrType.Name,
                EntityId = TryGetEntityId(pending.Entry),
                OldValuesJson = pending.OldValuesJson,
                NewValuesJson = pending.Action == AuditAction.Purge
                    ? null
                    : valueSerializer.Serialize(pending.Entry, originalValues: false),
                IpAddress = requestMetadataService.IpAddress,
                UserAgent = requestMetadataService.UserAgent,
                CreatedAt = DateTimeOffset.UtcNow
            });

            count++;
        }

        return count;
    }

    private static Guid? TryGetEntityId(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count != 1)
            return null;

        var keyProperty = primaryKey.Properties[0];
        var value = entry.Property(keyProperty.Name).CurrentValue;
        return value is Guid id && id != Guid.Empty ? id : null;
    }
}
