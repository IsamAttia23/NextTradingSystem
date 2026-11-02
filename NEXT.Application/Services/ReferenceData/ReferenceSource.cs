using NEXT.Application.DTOs.Common;
using System.Linq.Expressions;

using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;

namespace NEXT.Application.Services.ReferenceData;

public sealed class ReferenceSource<TEntity>(string key, IRepository<TEntity> repository, IQueryExecutor executor,
    Expression<Func<TEntity, ReferenceOptionDto>> selector) : IReferenceSource where TEntity : NEXT.Domain.Common.Entities.AuditableEntity<Guid>
{
    public string Key => key;
    public Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(CancellationToken ct) =>
        executor.ToListAsync(repository.FindAsync(entity => true, ct).OrderBy(x => x.Id).Select(selector), ct);
}
