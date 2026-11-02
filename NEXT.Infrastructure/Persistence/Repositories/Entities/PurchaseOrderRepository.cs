using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class PurchaseOrderRepository(NEXTDbContext context) : KeyRepository<PurchaseOrder, Guid>(context), IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => StorageErrors.ExecuteAsync(() => Context.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id, cancellationToken));

    public override Expression<Func<PurchaseOrder, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as PurchaseOrderQueryModel ?? new PurchaseOrderQueryModel { SearchTerm = query.SearchTerm });
    }

    public Expression<Func<PurchaseOrder, bool>> BuildFilterExpression(PurchaseOrderQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Expression<Func<PurchaseOrder, bool>> filters = entity =>
            (!query.From.HasValue || entity.OrderDate >= query.From.Value) &&
            (!query.To.HasValue || entity.OrderDate <= query.To.Value) &&
            (!query.SupplierId.HasValue || entity.SupplierId == query.SupplierId.Value) &&
            (!query.Status.HasValue || entity.Status == query.Status.Value);

        return CombineSearchExpression(filters, query.SearchTerm, "Number", "Notes");
    }

    public override IQueryable<PurchaseOrder> ApplySorting(IQueryable<PurchaseOrder> source, string? sortBy, SortDirection? sortDirection)
    {
        var descending = sortDirection == SortDirection.Descending;
        return ThenByField(OrderByField(source, "OrderDate", descending), "Id", descending);
    }
}
