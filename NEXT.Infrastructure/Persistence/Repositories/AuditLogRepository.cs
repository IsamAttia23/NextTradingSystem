using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for AuditLog; inherits common CRUD behavior from the generic repository.</summary>
public sealed class AuditLogRepository(NEXTDbContext context) : Repository<AuditLog, Guid>(context), IAuditLogRepository
{
    public Task<List<AuditLog>> SearchAsync(DateTimeOffset? from, DateTimeOffset? to,   Guid? userId, string? entityType, int skip, int take, CancellationToken cancellationToken = default) { var q=context.AuditLogs.AsNoTracking().Include(x=>x.User).AsQueryable(); if(from.HasValue) q=q.Where(x=>x.CreatedAt>=from); if(to.HasValue) q=q.Where(x=>x.CreatedAt<to); if(userId.HasValue) q=q.Where(x=>x.UserId==userId); if(!string.IsNullOrWhiteSpace(entityType)) q=q.Where(x=>x.EntityType==entityType); return q.OrderByDescending(x=>x.CreatedAt).Skip(Math.Max(0,skip)).Take(Math.Clamp(take,1,200)).ToListAsync(cancellationToken); }
}
