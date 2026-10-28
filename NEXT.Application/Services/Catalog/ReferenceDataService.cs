using System.Linq.Expressions;
using NEXT.Application.DTOs.Catalog;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.Services.Catalog;
public sealed class ReferenceDataService(IEnumerable<IReferenceSource> sources) : IReferenceDataService
{
    public Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(string key, CancellationToken ct = default) =>
        sources.Single(x => x.Key == key).GetAsync(ct);
}
public sealed class ReferenceSource<TEntity>(string key, IRepository<TEntity> repository, IPaginationServices pagination,
    Expression<Func<TEntity, ReferenceOptionDto>> selector) : IReferenceSource where TEntity : NEXT.Domain.Common.Entities.AuditableEntity<Guid>
{
    public string Key => key;
    public async Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(CancellationToken ct)
    {
        List<ReferenceOptionDto> options = [];
        int page = 1;
        TPaginationQueryModel<ReferenceOptionDto> result;
        do
        {
            ct.ThrowIfCancellationRequested();
            result = await pagination.GetPaggedList(repository.Query().OrderBy(x => x.Id).Select(selector), new PaginationQuery { PageNumber = page++, PageSize = 50 }, ct);
            options.AddRange(result.Data);
        } while (page <= result.TotalPages);
        return options;
    }
}
