using NEXT.Application.DTOs.Accounting;
namespace NEXT.web.Models;
public sealed record FinancialPeriodsView(IReadOnlyList<FinancialPeriodView> Periods, IReadOnlyList<PeriodEventView> Events);
