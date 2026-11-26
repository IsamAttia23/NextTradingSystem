using NEXT.Domain.Common.Entities;
namespace NEXT.Domain.Entities;
public sealed class FinancialPeriod : AuditableEntity<Guid>
{
    private FinancialPeriod() { }
    public string Name { get; private set; } = "";
    public DateOnly Start { get; private set; }
    public DateOnly End { get; private set; }
    public bool IsClosed { get; private set; }
    public long Version { get; private set; }
    public static FinancialPeriod Create(string name, DateOnly start, DateOnly end) => new()
        { Id = Guid.NewGuid(), Name = name, Start = start, End = end, Version = 1 };
    public void Change(bool closed) { IsClosed = closed; Version++; }
}
