using NEXT.Application.DTOs.Presentation;

namespace NEXT.Application.Features.Common.CQRS;

public interface IActivityPageEnricher
{
    Task<ActivityPageDto> EnrichAsync(ActivityPageDto page, CancellationToken ct);
}
