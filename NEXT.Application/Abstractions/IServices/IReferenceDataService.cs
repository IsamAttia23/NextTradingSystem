using NEXT.Application.DTOs.Common;

namespace NEXT.Application.Abstractions.IServices;
public interface IReferenceDataService
{
    Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(string key, CancellationToken ct = default);
}
