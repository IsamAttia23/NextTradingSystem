using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class StockMovementRepository(NEXTDbContext context) : KeyRepository<StockMovement, Guid>(context), IStockMovementRepository
{
    public override Expression<Func<StockMovement, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as StockMovementQueryModel ?? new StockMovementQueryModel
        {
            SearchTerm = query.SearchTerm
        });
    }

    public Expression<Func<StockMovement, bool>> BuildFilterExpression(StockMovementQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Expression<Func<StockMovement, bool>> filters = entity =>
            (!query.ProductId.HasValue || entity.ProductId == query.ProductId.Value) &&
            (!query.WarehouseId.HasValue || entity.WarehouseId == query.WarehouseId.Value) &&
            (!query.Type.HasValue || entity.Type == query.Type.Value) &&
            (!query.From.HasValue || entity.CreatedAt >= query.From.Value) &&
            (!query.To.HasValue || entity.CreatedAt < query.To.Value);
        return CombineSearchExpression(filters, query.SearchTerm, "Product.Name", "Warehouse.Name", "Notes");
    }

    public override IQueryable<StockMovement> ApplySorting(IQueryable<StockMovement> source, string? sortBy, SortDirection? sortDirection)
    {
        var descending = sortDirection == SortDirection.Descending;
        return ThenByField(OrderByField(source, "CreatedAt", descending), "Id", descending);
    }
}
