using NEXT.Application.DTOs.Accounting;
namespace NEXT.web.Models;

public sealed record ApproveOpeningView(OpeningPosition Position, DateOnly Today);
