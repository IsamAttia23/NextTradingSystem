using NEXT.Domain.Entities;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for JournalEntry. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IJournalEntryRepository : IKeyRepository<JournalEntry, Guid>
{
    Task<JournalEntry?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
   IQueryable<JournalEntry> Search(JournalEntryQueryModel queryModel, CancellationToken cancellationToken = default);
}