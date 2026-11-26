using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed record PeriodEventView(Guid PeriodId, bool IsClosed, string Reason, string ActorName, DateTimeOffset At, long Version);
