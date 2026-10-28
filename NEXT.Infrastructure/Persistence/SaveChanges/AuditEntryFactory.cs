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
    AuditValueSerializer valueSerializer, IDeletionAuditContext? deletionAudit = null)
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
                NewValuesJson = Values(pending),
                IpAddress = requestMetadataService.IpAddress,
                UserAgent = requestMetadataService.UserAgent,
                CreatedAt = DateTimeOffset.UtcNow
            });

            count++;
        }

        return count;
    }

    private string? Values(PendingAuditEntry pending)
    {
        var json = pending.Action == AuditAction.Purge ? null : valueSerializer.Serialize(pending.Entry, originalValues: false);
        if (pending.Action is not (AuditAction.Delete or AuditAction.Purge) || deletionAudit?.Reason is not {} reason) return json;
        var values = json is null ? new System.Text.Json.Nodes.JsonObject() : System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        values["DeletionReason"] = reason;
        return values.ToJsonString();
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
