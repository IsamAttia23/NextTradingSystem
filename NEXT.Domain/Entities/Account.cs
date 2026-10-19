using NEXT.Domain.Common;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class Account : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Account? Parent { get; set; }
    public ICollection<Account> Children { get; set; } = new List<Account>();
    public AccountType AccountType { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
    public string Code { get; set; } = string.Empty;
    public ICollection<JournalLine> JournalLines { get; set; } = new List<JournalLine>();
}
