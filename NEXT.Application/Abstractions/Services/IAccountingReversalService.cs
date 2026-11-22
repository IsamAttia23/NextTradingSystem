using NEXT.Application.DTOs.Accounting;

namespace NEXT.Application.Abstractions.Services;

public interface IAccountingReversalService
{
    Task<Guid> ReverseAsync(ReverseEntryInput input, CancellationToken ct);
}
