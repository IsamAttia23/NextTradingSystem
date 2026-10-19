using NEXT.Domain.Common;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class UnitOfMeasure : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public Guid? BaseUnitId { get; set; }
    public UnitOfMeasure? BaseUnit { get; set; }
    public decimal ConversionFactor { get; set; } = 1m;
    public ICollection<UnitOfMeasure> DerivedUnits { get; set; } = new List<UnitOfMeasure>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
