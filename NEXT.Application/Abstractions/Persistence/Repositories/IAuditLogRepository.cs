using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.QueryModels;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IAuditLogRepository : IKeyRepository<AuditLog, Guid>
{
    Expression<Func<AuditLog, bool>> BuildFilterExpression(AuditLogQueryModel query);
}
