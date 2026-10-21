using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;
using NEXT.Domain.Queries;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for AuditLog. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IAuditLogRepository : IRepository<AuditLog, Guid>
{
    IQueryable<AuditLog> SearchAsync(AuditLogQueryModel queryModel, CancellationToken cancellationToken = default);
}
