using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class CustomerContact : AuditableEntity<Guid>
{
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}
