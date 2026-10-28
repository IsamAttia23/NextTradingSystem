using NEXT.Application.DTOs.Catalog;
namespace NEXT.Application.Abstractions.IServices;
public interface IReferenceSource
{
    string Key { get; }
    Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(CancellationToken ct);
}
public interface IReferenceDataService
{
    Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(string key, CancellationToken ct = default);
}
