using Microsoft.AspNetCore.Identity;

namespace NEXT.Domain.Entities;

public class UserRole : IdentityUserRole<Guid>
{
    public ApplicationUser User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
