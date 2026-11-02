using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IRecycleBinEntryRepository : IRepository<RecycleBinEntry>
{
    Task<List<RecycleBinEntry>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
