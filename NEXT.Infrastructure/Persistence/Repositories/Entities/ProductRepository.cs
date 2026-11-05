using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class ProductRepository(NEXTDbContext context) : KeyRepository<Product, Guid>(context), IProductRepository
{
    public IQueryable<Product> QueryIncludingDeleted() => Context.Products.IgnoreQueryFilters().AsTracking();

    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => StorageErrors.ExecuteAsync(() => Context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Barcode == barcode, cancellationToken));

    public Task<List<Product>> GetLowStockAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var q = Context.Products.AsNoTracking()
            .Include(x => x.Stocks)
            .Where(x => x.IsActive && x.Stocks
            .Any(s => s.Quantity - s.ReservedQuantity <= x.MinStock));
        if (warehouseId.HasValue)
        {
            q = q.Where(x => x.Stocks
        .Any(s => s.WarehouseId == warehouseId && s.Quantity - s.ReservedQuantity <= x.MinStock));
        }
        return StorageErrors.ExecuteAsync(() => q.OrderBy(x => x.Name).ToListAsync(cancellationToken));
    }

    public override Expression<Func<Product, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as ProductQueryModel ?? new ProductQueryModel
        {
            SearchTerm = query.SearchTerm
        });
    }

    public Expression<Func<Product, bool>> BuildFilterExpression(ProductQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var barcode = query.Barcode?.Trim();
        var name = query.Name?.Trim();
        var description = query.Description?.Trim();
        Expression<Func<Product, bool>> filters = entity =>
            (string.IsNullOrEmpty(barcode) || (entity.Barcode != null && entity.Barcode.Contains(barcode))) &&
            (string.IsNullOrEmpty(name) || entity.Name.Contains(name)) &&
            (string.IsNullOrEmpty(description) || (entity.Description != null && entity.Description.Contains(description))) &&
            (!query.CategoryId.HasValue || entity.CategoryId == query.CategoryId.Value) &&
            (!query.UnitOfMeasureId.HasValue || entity.UnitOfMeasureId == query.UnitOfMeasureId.Value) &&
            (!query.IsActive.HasValue || entity.IsActive == query.IsActive.Value) &&
            (!query.HasExpiry.HasValue || entity.HasExpiry == query.HasExpiry.Value) &&
            (!query.MinCostPrice.HasValue || entity.CostPrice >= query.MinCostPrice.Value) &&
            (!query.MaxCostPrice.HasValue || entity.CostPrice <= query.MaxCostPrice.Value) &&
            (!query.MinRetailPrice.HasValue || entity.RetailPrice >= query.MinRetailPrice.Value) &&
            (!query.MaxRetailPrice.HasValue || entity.RetailPrice <= query.MaxRetailPrice.Value) &&
            (!query.MinWholesalePrice.HasValue || entity.WholesalePrice >= query.MinWholesalePrice.Value) &&
            (!query.MaxWholesalePrice.HasValue || entity.WholesalePrice <= query.MaxWholesalePrice.Value) &&
            (!query.MinVipPrice.HasValue || entity.VipPrice >= query.MinVipPrice.Value) &&
            (!query.MaxVipPrice.HasValue || entity.VipPrice <= query.MaxVipPrice.Value) &&
            (!query.MinTaxRate.HasValue || entity.TaxRate >= query.MinTaxRate.Value) &&
            (!query.MaxTaxRate.HasValue || entity.TaxRate <= query.MaxTaxRate.Value) &&
            (!query.MinStock.HasValue || entity.MinStock >= query.MinStock.Value) &&
            (!query.MaxStock.HasValue || (entity.MaxStock.HasValue && entity.MaxStock.Value <= query.MaxStock.Value));
        return CombineSearchExpression(filters, query.SearchTerm, "Name", "Barcode", "Description");
    }

    private static readonly string[] SortFields = ["Name", "Barcode", "CostPrice", "RetailPrice", "WholesalePrice", "VipPrice"];
    public override IQueryable<Product> ApplySorting(IQueryable<Product> source, string? sortBy, SortDirection? sortDirection)
    {
        var selected = SortFields.FirstOrDefault(field => string.Equals(field, sortBy?.Trim(), StringComparison.OrdinalIgnoreCase));
        var descending = selected is not null && sortDirection == SortDirection.Descending;
        return ThenByField(OrderByField(source, selected ?? "Name", descending), "Id", descending);
    }
}
