using Microsoft.AspNetCore.Identity;

namespace NEXT.Domain.Entities;

public class Role : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
