namespace NEXT.Domain.Services;
/// <summary>Moving weighted average valuation at the database's monetary precision.</summary>
public static class InventoryValuation
{
    public static decimal ReceiptValue(decimal quantity, decimal unitCost)
    {
        if (quantity <= 0 || quantity > MaximumQuantity || !MoneyRules.IsValid(unitCost))
            throw new ArgumentOutOfRangeException(nameof(quantity));
        var value = MoneyRules.Round(quantity * unitCost);
        if (!MoneyRules.IsValid(value)) throw new ArgumentOutOfRangeException(nameof(unitCost));
        return value;
    }

    public static decimal IssueValue(decimal quantity, decimal carryingValue, decimal issued)
    {
        if (quantity <= 0 || issued <= 0 || issued > quantity || !MoneyRules.IsValid(carryingValue))
            throw new ArgumentOutOfRangeException(nameof(issued));
        // The last unit takes the exact remainder, including all previous rounding differences.
        return issued == quantity ? carryingValue : Math.Min(carryingValue, MoneyRules.Round(carryingValue * issued / quantity));
    }

    public static decimal Average(decimal quantity, decimal carryingValue) => quantity == 0 ? 0 : MoneyRules.Round(carryingValue / quantity);

    public const decimal MaximumQuantity = 999999999999.999m;
    public static decimal ReceiveCost(decimal quantity, decimal averageCost, decimal incoming, decimal unitCost)
    {
        if (quantity < 0
            || incoming <= 0
            || unitCost < 0
            || quantity + incoming > MaximumQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(incoming));
        }
        return decimal.Round((quantity * averageCost + incoming * unitCost) / (quantity + incoming), 2, MidpointRounding.AwayFromZero);
    }
}
