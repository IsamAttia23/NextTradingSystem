using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed record CashReconciliationView(Guid Id, AccountType Account, DateOnly Date, decimal BookBalance, decimal ExternalBalance,
    decimal DepositsInTransit, decimal OutstandingPayments, decimal Difference, string Evidence, string Reason, string ActorName, bool Stale);
