using NEXT.Application.DTOs.Common;

namespace NEXT.Application.Abstractions.IServices;

public interface IReferenceSource
{
    string Key { get; }
    Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(CancellationToken ct);
}
