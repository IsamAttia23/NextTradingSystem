using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed record DebtBookAccount(Guid Id, Guid CustomerId, string Name, string? Phone, bool Active, byte[] RowVersion, int CycleNumber = 1, DateTimeOffset? ClosedAt = null);
