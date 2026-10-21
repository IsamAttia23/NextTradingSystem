using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Product; inherits common CRUD behavior from the generic repository.</summary>
public sealed class ProductRepository(NEXTDbContext context) : TRepository<Product, Guid>(context), IProductRepository
{
    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Barcode == barcode, cancellationToken);
    public IQueryable<Product> Search(ProductQueryModel queryModel)
    {
        ArgumentNullException.ThrowIfNull(queryModel);

        var products = context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.UnitOfMeasure)
            .AsQueryable();

        products = ApplySearchTerm(products, queryModel);
        products = ApplyIdentityFilters(products, queryModel);
        products = ApplyPriceFilters(products, queryModel);
        products = ApplyTaxFilters(products, queryModel);
        products = ApplyStockFilters(products, queryModel);
        products = ApplyStatusFilters(products, queryModel);
        products = ApplySorting(products, queryModel.SortBy, queryModel.SortDirection);

        return products;
    }

    public Task<List<Product>> GetLowStockAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var q = context.Products.AsNoTracking()
            .Include(x => x.Stocks)
            .Where(x => x.IsActive && x.Stocks
            .Any(s => s.Quantity - s.ReservedQuantity <= x.MinStock));

        if (warehouseId.HasValue) q = q.Where(x => x.Stocks
        .Any(s => s.WarehouseId == warehouseId && s.Quantity - s.ReservedQuantity <= x.MinStock));
        return q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    // General search
    private static IQueryable<Product> ApplySearchTerm(IQueryable<Product> products, ProductQueryModel queryModel)
    {
        if (string.IsNullOrWhiteSpace(queryModel.SearchTerm))
            return products;

        var term = queryModel.SearchTerm.Trim();

        return products.Where(p =>
            p.Name.Contains(term) ||
            (p.Barcode != null && p.Barcode.Contains(term)) ||
            (p.Description != null && p.Description.Contains(term)));
    }

    // Barcode, name, description, category and unit
    private static IQueryable<Product> ApplyIdentityFilters(IQueryable<Product> products, ProductQueryModel queryModel)
    {
        if (!string.IsNullOrWhiteSpace(queryModel.Barcode))
        {
            var barcode = queryModel.Barcode.Trim();
            products = products.Where(p =>
                p.Barcode != null && p.Barcode.Contains(barcode));
        }

        if (!string.IsNullOrWhiteSpace(queryModel.Name))
        {
            var name = queryModel.Name.Trim();
            products = products.Where(p => p.Name.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(queryModel.Description))
        {
            var description = queryModel.Description.Trim();
            products = products.Where(p =>
                p.Description != null &&
                p.Description.Contains(description));
        }

        if (queryModel.CategoryId.HasValue)
        {
            products = products.Where(p =>
                p.CategoryId == queryModel.CategoryId.Value);
        }

        if (queryModel.UnitOfMeasureId.HasValue)
        {
            products = products.Where(p =>
                p.UnitOfMeasureId == queryModel.UnitOfMeasureId.Value);
        }

        return products;
    }

    // Cost, retail, wholesale and VIP prices
    private static IQueryable<Product> ApplyPriceFilters(IQueryable<Product> products, ProductQueryModel queryModel)
    {
        if (queryModel.MinCostPrice.HasValue)
            products = products.Where(p =>
                p.CostPrice >= queryModel.MinCostPrice.Value);

        if (queryModel.MaxCostPrice.HasValue)
            products = products.Where(p =>
                p.CostPrice <= queryModel.MaxCostPrice.Value);

        if (queryModel.MinRetailPrice.HasValue)
            products = products.Where(p =>
                p.RetailPrice >= queryModel.MinRetailPrice.Value);

        if (queryModel.MaxRetailPrice.HasValue)
            products = products.Where(p =>
                p.RetailPrice <= queryModel.MaxRetailPrice.Value);

        if (queryModel.MinWholesalePrice.HasValue)
            products = products.Where(p =>
                p.WholesalePrice >= queryModel.MinWholesalePrice.Value);

        if (queryModel.MaxWholesalePrice.HasValue)
            products = products.Where(p =>
                p.WholesalePrice <= queryModel.MaxWholesalePrice.Value);

        if (queryModel.MinVipPrice.HasValue)
            products = products.Where(p =>
                p.VipPrice >= queryModel.MinVipPrice.Value);

        if (queryModel.MaxVipPrice.HasValue)
            products = products.Where(p =>
                p.VipPrice <= queryModel.MaxVipPrice.Value);

        return products;
    }

    // Tax rate
    private static IQueryable<Product> ApplyTaxFilters(IQueryable<Product> products, ProductQueryModel queryModel)
    {
        if (queryModel.MinTaxRate.HasValue)
            products = products.Where(p =>
                p.TaxRate >= queryModel.MinTaxRate.Value);

        if (queryModel.MaxTaxRate.HasValue)
            products = products.Where(p =>
                p.TaxRate <= queryModel.MaxTaxRate.Value);

        return products;
    }

    // Configured minimum and maximum stock thresholds
    private static IQueryable<Product> ApplyStockFilters(IQueryable<Product> products, ProductQueryModel queryModel)
    {
        if (queryModel.MinStock.HasValue)
            products = products.Where(p =>
                p.MinStock >= queryModel.MinStock.Value);

        if (queryModel.MaxStock.HasValue)
            products = products.Where(p =>
                p.MaxStock.HasValue &&
                p.MaxStock.Value <= queryModel.MaxStock.Value);

        return products;
    }

    // Active status and expiry
    private static IQueryable<Product> ApplyStatusFilters(IQueryable<Product> products, ProductQueryModel queryModel)
    {
        if (queryModel.IsActive.HasValue)
            products = products.Where(p =>
                p.IsActive == queryModel.IsActive.Value);

        if (queryModel.HasExpiry.HasValue)
            products = products.Where(p =>
                p.HasExpiry == queryModel.HasExpiry.Value);

        return products;
    }

    // Sorting
    private static IQueryable<Product> ApplySorting(IQueryable<Product> products, string? sortBy, SortDirection direction)
    {
        var descending = direction == SortDirection.Descending;

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "name" => descending
                ? products.OrderByDescending(p => p.Name).ThenByDescending(p => p.Id)
                : products.OrderBy(p => p.Name).ThenBy(p => p.Id),

            "barcode" => descending
                ? products.OrderByDescending(p => p.Barcode).ThenByDescending(p => p.Id)
                : products.OrderBy(p => p.Barcode).ThenBy(p => p.Id),

            "costprice" => descending
                ? products.OrderByDescending(p => p.CostPrice).ThenByDescending(p => p.Id)
                : products.OrderBy(p => p.CostPrice).ThenBy(p => p.Id),

            "retailprice" => descending
                ? products.OrderByDescending(p => p.RetailPrice).ThenByDescending(p => p.Id)
                : products.OrderBy(p => p.RetailPrice).ThenBy(p => p.Id),

            "wholesaleprice" => descending
                ? products.OrderByDescending(p => p.WholesalePrice).ThenByDescending(p => p.Id)
                : products.OrderBy(p => p.WholesalePrice).ThenBy(p => p.Id),

            "vipprice" => descending
                ? products.OrderByDescending(p => p.VipPrice).ThenByDescending(p => p.Id)
                : products.OrderBy(p => p.VipPrice).ThenBy(p => p.Id),

            _ => products
                .OrderBy(p => p.Name)
                .ThenBy(p => p.Id)
        };
    }
}
