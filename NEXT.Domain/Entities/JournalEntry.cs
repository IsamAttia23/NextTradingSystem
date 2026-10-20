using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class JournalEntry : AuditableEntity<Guid>
{
    public string EntryNumber { get; set; } = string.Empty;
    public DateOnly EntryDate { get; set; }
    public string? Description { get; set; }
    public JournalReferenceType? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public byte[] RowVersion { get; set; }

    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}
