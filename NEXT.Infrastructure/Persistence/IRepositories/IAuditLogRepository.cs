using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for AuditLog. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IAuditLogRepository : IRepository<AuditLog, Guid>
{
    Task<List<AuditLog>> SearchAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? userId, string? entityType, int skip, int take, CancellationToken cancellationToken = default);
}
