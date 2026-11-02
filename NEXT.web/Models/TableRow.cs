using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Models;

public sealed record TableRow(IReadOnlyList<string> Cells, IReadOnlyList<UiAction> Actions,
    ColorBadgeDto? Badge = null, RecordIdentityDto? Identity = null, string? NameWarning = null);
