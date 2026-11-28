namespace NEXT.Domain.Services;
public static class TrialInventoryValuation
{
    public static decimal Issue(decimal quantity, decimal value, decimal issued, decimal fallbackCost)
    {
        if (issued <= 0 || issued > InventoryValuation.MaximumQuantity || !MoneyRules.IsValid(Math.Abs(value)) || !MoneyRules.IsValid(fallbackCost))
            throw new ArgumentOutOfRangeException(nameof(issued));
        var available = Math.Max(quantity, 0);
        var existing = available > 0 ? InventoryValuation.IssueValue(available, Math.Max(value, 0), Math.Min(available, issued)) : 0;
        var cost = existing + (issued > available ? InventoryValuation.ReceiptValue(issued - available, fallbackCost) : 0);
        if (!MoneyRules.IsValid(cost)) throw new ArgumentOutOfRangeException(nameof(issued));
        return cost;
    }
    public static decimal ReceiptAdjustment(decimal quantity, decimal value, decimal incoming, decimal actualUnitCost)
    {
        if (quantity >= 0) return 0;
        var settled = Math.Min(-quantity, incoming);
        var estimated = InventoryValuation.IssueValue(-quantity, Math.Abs(value), settled);
        return InventoryValuation.ReceiptValue(settled, actualUnitCost) - estimated;
    }
}
