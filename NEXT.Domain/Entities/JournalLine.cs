using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class JournalLine : AuditableEntity<Guid>
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}
