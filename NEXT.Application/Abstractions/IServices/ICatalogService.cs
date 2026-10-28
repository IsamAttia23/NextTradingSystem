using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.Abstractions.IServices;
public interface ICatalogService<TDto, in TInput> where TDto : class where TInput : class
{
    Task<TPaginationQueryModel<TDto>> ListAsync(PaginationQuery query, CancellationToken ct = default);
    Task<TDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<TDto> CreateAsync(TInput input, CancellationToken ct = default);
    Task<TDto> UpdateAsync(Guid id, TInput input, CancellationToken ct = default);
    Task<NEXT.Application.DTOs.Presentation.ActivityPageDto> ActivityAsync(Guid id, string? kind, int page, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default, string? reason = null);
}
