using Microsoft.AspNetCore.Identity;
using NEXT.Domain.Common.Entities;

namespace NEXT.Domain.Entities
{
    public class ApplicationUser : IdentityUser<Guid>, IIsDeletableEntity
    {
        public string FullName { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? LastInvalidLoginAttempt { get; set; }
        public DateTimeOffset CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTimeOffset? UpdatedOn { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public Guid? DeletedBy { get; set; }
        public string? DeletionReason { get; set; }
        public byte[]? AvatarUrl { get; set; }
        public Guid BranchId { get; set; }

        public Branch? Branch { get; set; }
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
