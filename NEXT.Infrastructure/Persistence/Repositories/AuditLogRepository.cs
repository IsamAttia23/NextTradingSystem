using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.Domain.Queries;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for AuditLog; inherits common CRUD behavior from the generic repository.</summary>
public sealed class AuditLogRepository(NEXTDbContext context) : TRepository<AuditLog, Guid>(context), IAuditLogRepository
{
    public IQueryable<AuditLog> SearchAsync(AuditLogQueryModel queryModel, CancellationToken cancellationToken = default)
    {
        var q = context.AuditLogs.AsNoTracking().Include(x => x.User).AsQueryable();
        if (queryModel.From.HasValue) q = q.Where(x => x.CreatedAt >= queryModel.From);
        if (queryModel.To.HasValue) q = q.Where(x => x.CreatedAt < queryModel.To);
        if (queryModel.UserId.HasValue) q = q.Where(x => x.UserId == queryModel.UserId);
        if (!string.IsNullOrWhiteSpace(queryModel.EntityType)) q = q.Where(x => x.EntityType == queryModel.EntityType);
        return q.OrderByDescending(x => x.CreatedAt).AsQueryable();
    }
}
