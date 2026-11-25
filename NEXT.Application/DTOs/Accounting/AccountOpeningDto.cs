namespace NEXT.Application.DTOs.Accounting;

public sealed record AccountOpeningDto(DateOnly Date, decimal Before, decimal Verified, decimal Difference, string Reason, string Actor);
