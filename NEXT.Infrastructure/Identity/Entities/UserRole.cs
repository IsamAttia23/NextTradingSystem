using Microsoft.AspNetCore.Identity;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Identity.Entities;

public class UserRole : IdentityUserRole<Guid>
{
    public ApplicationUser User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
