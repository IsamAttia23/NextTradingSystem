using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for JournalEntry. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IJournalEntryRepository : IRepository<JournalEntry, Guid>
{
    Task<JournalEntry?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<JournalEntry>> SearchAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
