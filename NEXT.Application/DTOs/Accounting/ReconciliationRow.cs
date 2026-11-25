using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed record ReconciliationRow(AccountType Account, decimal Book, decimal Source)
{
    public decimal Difference => Book - Source;
}
