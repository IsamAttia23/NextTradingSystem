using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for StockMovement; inherits common CRUD behavior from the generic repository.</summary>
public sealed class StockMovementRepository(NEXTDbContext context) : TRepository<StockMovement, Guid>(context), IStockMovementRepository
{
    public IQueryable<StockMovement> GetForProduct(StockMovementQueryModel queryModel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryModel);

        var query = context.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .AsQueryable();

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
        return query.Where(x => x.ProductId == queryModel.ProductId);
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
