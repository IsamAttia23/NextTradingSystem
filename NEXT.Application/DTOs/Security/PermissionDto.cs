using NEXT.Domain.Common.Entities;

namespace NEXT.Application.DTOs.Security;

public sealed class PermissionDto : TIdType<Guid>
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
}