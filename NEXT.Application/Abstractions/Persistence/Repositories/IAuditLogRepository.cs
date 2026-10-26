using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Domain.Queries;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for AuditLog. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IAuditLogRepository : IKeyRepository<AuditLog, Guid>
{
    IQueryable<AuditLog> SearchAsync(AuditLogQueryModel queryModel, CancellationToken cancellationToken = default);
}
