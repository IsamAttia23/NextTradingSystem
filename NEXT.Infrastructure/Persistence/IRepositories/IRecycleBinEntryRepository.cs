using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for RecycleBinEntry. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IRecycleBinEntryRepository : IRepository<RecycleBinEntry>
{
    Task<List<RecycleBinEntry>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
