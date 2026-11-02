using System.Linq.Expressions;
using NEXT.Application.Common.QueryModels;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for ProductBatch; inherits common CRUD behavior from the generic repository.</summary>
public sealed class ProductBatchRepository(NEXTDbContext context) : KeyRepository<ProductBatch, Guid>(context), IProductBatchRepository
{
    public override Expression<Func<ProductBatch, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parameter = Expression.Parameter(typeof(ProductBatch), "entity");
        return Expression.Lambda<Func<ProductBatch, bool>>(
            BuildSearchExpression(query.SearchTerm, parameter, "BatchNumber", "Product.Name", "Warehouse.Name"), parameter);
    }


}
