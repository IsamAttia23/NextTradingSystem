using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class AuditLogRepository(NEXTDbContext context) : KeyRepository<AuditLog, Guid>(context), IAuditLogRepository
{
    public override Expression<Func<AuditLog, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as AuditLogQueryModel ?? new AuditLogQueryModel { SearchTerm = query.SearchTerm });
    }

    public Expression<Func<AuditLog, bool>> BuildFilterExpression(AuditLogQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Expression<Func<AuditLog, bool>> filters = entity =>
            (!query.From.HasValue || entity.CreatedAt >= query.From.Value) &&
            (!query.To.HasValue || entity.CreatedAt < query.To.Value) &&
            (!query.UserId.HasValue || entity.UserId == query.UserId.Value) &&
            (string.IsNullOrWhiteSpace(query.EntityType) || entity.EntityType == query.EntityType);

        return CombineSearchExpression(filters, query.SearchTerm, "EntityType", "OldValuesJson", "NewValuesJson");
    }

    public override IQueryable<AuditLog> ApplySorting(IQueryable<AuditLog> entities, string? sortBy, SortDirection? sortDirection)
        => base.ApplySorting(entities, string.IsNullOrWhiteSpace(sortBy) ? nameof(AuditLog.CreatedAt) : sortBy,
            sortDirection ?? SortDirection.Descending);
}
