using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed record CashPosition(AccountType Account, DateOnly Date, decimal Balance, string Hash);
