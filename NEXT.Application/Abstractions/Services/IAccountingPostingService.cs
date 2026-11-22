using NEXT.Application.DTOs.Accounting;

namespace NEXT.Application.Abstractions.Services;

public interface IAccountingPostingService
{
    Task<Guid> PostAsync(PostingRequestDto input, CancellationToken ct);
}
