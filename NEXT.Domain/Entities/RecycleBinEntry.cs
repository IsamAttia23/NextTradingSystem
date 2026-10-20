using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class RecycleBinEntry : TIdType<Guid>
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityDataJson { get; set; } = string.Empty;
    public Guid? DeletedBy { get; set; }
    public DateTimeOffset DeletedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DeleteReason { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
