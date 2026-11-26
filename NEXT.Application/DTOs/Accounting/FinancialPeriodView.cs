using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed record FinancialPeriodView(Guid Id, string Name, DateOnly Start, DateOnly End, bool IsClosed, long Version);
