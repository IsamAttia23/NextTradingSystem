namespace NEXT.Application.DTOs.Accounting;

public sealed record InventoryPostingState(decimal TotalCost, int MovementCount, bool IsValidSource,
    bool IsOpening, Guid? SupplierId);
