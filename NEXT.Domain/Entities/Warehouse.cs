using NEXT.Domain.Common;

namespace NEXT.Domain.Entities;

public class Warehouse : IsDeletableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
