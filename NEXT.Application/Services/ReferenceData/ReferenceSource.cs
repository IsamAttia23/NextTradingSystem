using NEXT.Application.DTOs.Common;
using Mapster;

using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Services.ReferenceData;

public sealed class ReferenceSource<TEntity>(string key, IRepository<TEntity> repository, IQueryExecutor executor,
    TypeAdapterConfig mapping) : IReferenceSource where TEntity : NEXT.Domain.Common.Entities.AuditableEntity<Guid>
{
    public string Key => key;
    public Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(CancellationToken ct) =>
        executor.ToListAsync(repository.FindAsync(entity => true, ct).OrderBy(x => x.Id).ProjectToType<ReferenceOptionDto>(mapping), ct);
}
