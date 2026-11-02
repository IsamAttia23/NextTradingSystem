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
        => StorageErrors.ExecuteAsync(() => Context.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, cancellationToken));

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

    private static readonly string[] SortFields = ["Name", "Code"];
    public override IQueryable<Supplier> ApplySorting(IQueryable<Supplier> source, string? sortBy, SortDirection? sortDirection)
    {
        var selected = SortFields.FirstOrDefault(field => string.Equals(field, sortBy?.Trim(), StringComparison.OrdinalIgnoreCase));
        var descending = sortDirection == SortDirection.Descending;
        return ThenByField(OrderByField(source, selected ?? "Name", descending), "Id");
    }
}
