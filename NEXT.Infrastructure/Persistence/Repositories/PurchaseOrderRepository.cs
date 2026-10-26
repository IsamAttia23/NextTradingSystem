using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for PurchaseOrder; inherits common CRUD behavior from the generic repository.</summary>
public sealed class PurchaseOrderRepository(NEXTDbContext context) : KeyRepository<PurchaseOrder, Guid>(context), IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => context.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public IQueryable<PurchaseOrder> Search(PurchaseOrderQueryModel queryModel)
    {
        ArgumentNullException.ThrowIfNull(queryModel);

        var orders = context.PurchaseOrders
            .AsNoTracking()
            .Include(x => x.Supplier)
            .AsQueryable();

        orders = ApplyDateFilters(orders, queryModel);
        orders = ApplySupplierFilter(orders, queryModel);
        orders = ApplyStatusFilter(orders, queryModel);

        return ApplySorting(
            orders,
            queryModel.SortBy,
            queryModel.SortDirection);
    }

    // Filter by order date
    private static IQueryable<PurchaseOrder> ApplyDateFilters(IQueryable<PurchaseOrder> orders, PurchaseOrderQueryModel queryModel)
    {
        if (queryModel.From.HasValue)
        {
            orders = orders.Where(x =>
                x.OrderDate >= queryModel.From.Value);
        }

        if (queryModel.To.HasValue)
        {
            orders = orders.Where(x =>
                x.OrderDate <= queryModel.To.Value);
        }

        return orders;
    }

    // Filter by supplier
    private static IQueryable<PurchaseOrder> ApplySupplierFilter(IQueryable<PurchaseOrder> orders, PurchaseOrderQueryModel queryModel)
    {
        if (queryModel.SupplierId.HasValue)
        {
            orders = orders.Where(x =>
                x.SupplierId == queryModel.SupplierId.Value);
        }

        return orders;
    }

    // Filter by status
    private static IQueryable<PurchaseOrder> ApplyStatusFilter(IQueryable<PurchaseOrder> orders, PurchaseOrderQueryModel queryModel)
    {
        if (queryModel.Status.HasValue)
        {
            orders = orders.Where(x =>
                x.Status == queryModel.Status.Value);
        }

        return orders;
    }

    // Sorting
    private static IQueryable<PurchaseOrder> ApplySorting(IQueryable<PurchaseOrder> orders, string? sortBy, SortDirection? direction)
    {
        var descending = direction == SortDirection.Descending;

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "orderdate" => descending
                ? orders.OrderByDescending(x => x.OrderDate)
                    .ThenByDescending(x => x.Id)
                : orders.OrderBy(x => x.OrderDate)
                    .ThenBy(x => x.Id),

            _ => descending
                ? orders.OrderByDescending(x => x.OrderDate)
                    .ThenByDescending(x => x.Id)
                : orders.OrderBy(x => x.OrderDate)
                    .ThenBy(x => x.Id)
        };
    }
}
