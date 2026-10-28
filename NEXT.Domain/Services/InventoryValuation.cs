namespace NEXT.Domain.Services;
/// <summary>Moving weighted average valuation at the database's monetary precision.</summary>
public static class InventoryValuation
{
    public const decimal MaximumQuantity = 999999999999.999m;
    public static decimal ReceiveCost(decimal quantity, decimal averageCost, decimal incoming, decimal unitCost)
    {
        if (quantity < 0 || incoming <= 0 || unitCost < 0 || quantity + incoming > MaximumQuantity)
            throw new ArgumentOutOfRangeException(nameof(incoming));
        return decimal.Round((quantity * averageCost + incoming * unitCost) / (quantity + incoming), 2, MidpointRounding.AwayFromZero);
    }
}
