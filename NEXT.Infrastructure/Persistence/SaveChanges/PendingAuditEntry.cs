using Microsoft.EntityFrameworkCore.ChangeTracking;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.SaveChanges;

/// <summary>Captures information that must survive the first database save.</summary>
internal sealed record PendingAuditEntry(
    EntityEntry Entry,
    AuditAction Action,
    string? OldValuesJson);
