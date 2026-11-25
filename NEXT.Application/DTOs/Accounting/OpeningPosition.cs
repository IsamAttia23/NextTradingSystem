using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed record OpeningPosition(AccountType Account, decimal BookBalance, decimal? InventoryValue, string Hash,
    DateOnly? LatestDate, bool Approved, DateTimeOffset? LatestStockAt = null);
