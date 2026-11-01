using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class SupplierRepository(NEXTDbContext context) : KeyRepository<Supplier, Guid>(context), ISupplierRepository
{
    public Task<Supplier?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Context.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

    public override Expression<Func<Supplier, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as SupplierQueryModel ?? new SupplierQueryModel { SearchTerm = query.SearchTerm });
    }

    public Expression<Func<Supplier, bool>> BuildFilterExpression(SupplierQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Expression<Func<Supplier, bool>> filters = entity =>
            (!query.IsActive.HasValue || entity.IsActive == query.IsActive.Value);

        return CombineSearchExpression(filters, query.SearchTerm, "Name", "Code", "Phone", "Email");
    }

    public override IQueryable<Supplier> ApplySorting(IQueryable<Supplier> query, string? sortBy, SortDirection? sortDirection)
    {
        var descending = sortDirection == SortDirection.Descending;

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "name" => descending
                ? query.OrderByDescending(x => x.Name).ThenBy(x => x.Id)
                : query.OrderBy(x => x.Name).ThenBy(x => x.Id),

            "code" => descending
                ? query.OrderByDescending(x => x.Code).ThenBy(x => x.Id)
                : query.OrderBy(x => x.Code).ThenBy(x => x.Id),

            _ => descending
                ? query.OrderByDescending(x => x.Name).ThenBy(x => x.Id)
                : query.OrderBy(x => x.Name).ThenBy(x => x.Id)
        };
    }
}
