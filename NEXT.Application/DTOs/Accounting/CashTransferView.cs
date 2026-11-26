using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed record CashTransferView(DateOnly Date, AccountType FromAccount, AccountType ToAccount, decimal Amount, string Reason, string ActorName);
