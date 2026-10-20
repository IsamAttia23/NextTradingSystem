using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class Branch : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}
