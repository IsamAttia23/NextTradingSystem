using NEXT.Application.DTOs.Accounting;
namespace NEXT.web.Models;

public sealed record FinancialAccountDetailsView(AccountSummaryDto Account, TableModel Movements,
    AccountOpeningDto? Opening, bool CanApproveOpening);
