using NEXT.Application.Common.QueryModels;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for StockMovement; inherits common CRUD behavior from the generic repository.</summary>
public sealed class StockMovementRepository(NEXTDbContext context) : KeyRepository<StockMovement, Guid>(context), IStockMovementRepository
{
    public override IQueryable<StockMovement> Search(PaginationQuery query) =>
        GetForProduct(query as StockMovementQueryModel ?? new StockMovementQueryModel { SearchTerm = query.SearchTerm, SortBy = query.SortBy,
            SortDirection = query.SortDirection, PageNumber = query.PageNumber, PageSize = query.PageSize });

    public IQueryable<StockMovement> GetForProduct(StockMovementQueryModel queryModel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryModel);

        var query = Context.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryModel.SearchTerm))
        {
            var term = queryModel.SearchTerm.Trim();
            query = query.Where(x => x.Product.Name.Contains(term) || x.Warehouse.Name.Contains(term) || (x.Notes != null && x.Notes.Contains(term)));
        }
        if (queryModel.Type.HasValue) query = query.Where(x => x.Type == queryModel.Type.Value);
        query = ApplyProductFilter(query, queryModel);
        query = ApplyWarehouseFilter(query, queryModel);
        query = ApplyDateFilters(query, queryModel);

        return ApplySorting(
            query,
            queryModel.SortBy,
            queryModel.SortDirection);
    }

    private static IQueryable<StockMovement> ApplyProductFilter(IQueryable<StockMovement> query, StockMovementQueryModel queryModel)
    {
        return queryModel.ProductId.HasValue ? query.Where(x => x.ProductId == queryModel.ProductId.Value) : query;
    }

    private static IQueryable<StockMovement> ApplyWarehouseFilter(IQueryable<StockMovement> query, StockMovementQueryModel queryModel)
    {
        if (queryModel.WarehouseId.HasValue)
        {
            query = query.Where(
                x => x.WarehouseId == queryModel.WarehouseId.Value);
        }

        return query;
    }

    private static IQueryable<StockMovement> ApplyDateFilters(IQueryable<StockMovement> query, StockMovementQueryModel queryModel)
    {
        if (queryModel.From.HasValue)
        {
            query = query.Where(
                x => x.CreatedAt >= queryModel.From.Value);
        }

        if (queryModel.To.HasValue)
        {
            query = query.Where(
                x => x.CreatedAt < queryModel.To.Value);
        }

        return query;
    }

    private static IQueryable<StockMovement> ApplySorting( IQueryable<StockMovement> query, string? sortBy,SortDirection? direction)
    {
        var descending = direction == SortDirection.Descending;

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "createdat" => descending
                ? query.OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id),

            _ => descending
                ? query.OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
        };
    }
}
