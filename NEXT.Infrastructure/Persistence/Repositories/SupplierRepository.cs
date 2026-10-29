using NEXT.Application.Common.QueryModels;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Supplier; inherits common CRUD behavior from the generic repository.</summary>
public sealed class SupplierRepository(NEXTDbContext context) : KeyRepository<Supplier, Guid>(context), ISupplierRepository
{
    public override IQueryable<Supplier> Search(PaginationQuery query) =>
        Search(query as SupplierQueryModel ?? new SupplierQueryModel { SearchTerm = query.SearchTerm, SortBy = query.SortBy,
            SortDirection = query.SortDirection, PageNumber = query.PageNumber, PageSize = query.PageSize });

    public Task<Supplier?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => Context.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

    public IQueryable<Supplier> Search(SupplierQueryModel queryModel)
    {
        ArgumentNullException.ThrowIfNull(queryModel);

        var query = Context.Suppliers
            .AsNoTracking()
            .AsQueryable();

        query = ApplySearchTerm(query, queryModel.SearchTerm);
        query = ApplyActiveFilter(query, queryModel.IsActive);

        return ApplySorting(
            query,
            queryModel.SortBy,
            queryModel.SortDirection);
    }

    private static IQueryable<Supplier> ApplySearchTerm(IQueryable<Supplier> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var term = searchTerm.Trim();

        return query.Where(x =>
            x.Name.Contains(term) ||
            (x.Code != null && x.Code.Contains(term)) ||
            (x.Phone != null && x.Phone.Contains(term)) ||
            (x.Email != null && x.Email.Contains(term)));
    }

    private static IQueryable<Supplier> ApplyActiveFilter(IQueryable<Supplier> query, bool? isActive)
    {
        if (isActive.HasValue)
        {
            query = query.Where(
                x => x.IsActive == isActive.Value);
        }

        return query;
    }

    private static IQueryable<Supplier> ApplySorting(IQueryable<Supplier> query, string? sortBy, SortDirection? direction)
    {
        var descending = direction == SortDirection.Descending;

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
