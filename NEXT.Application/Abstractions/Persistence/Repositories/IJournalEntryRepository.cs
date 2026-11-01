using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for JournalEntry. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IJournalEntryRepository : IKeyRepository<JournalEntry, Guid>
{
    Task<JournalEntry?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Expression<Func<JournalEntry, bool>> BuildFilterExpression(JournalEntryQueryModel query);
}
