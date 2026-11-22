namespace NEXT.Domain.Services;

public static class MoneyRules
{
    public const decimal Maximum = 9_999_999_999_999.99m;
    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static bool IsValid(decimal value) => value >= 0 && value <= Maximum && Round(value) == value;
    public static bool CanSettle(decimal remaining, decimal amount) =>
        IsValid(remaining) && IsValid(amount) && amount > 0 && amount <= remaining;
}
