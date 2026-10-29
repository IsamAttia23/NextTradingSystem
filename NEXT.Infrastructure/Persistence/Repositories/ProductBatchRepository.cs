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
    public override IQueryable<ProductBatch> Search(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var result = base.Search(new PaginationQuery { SortBy = query.SortBy, SortDirection = query.SortDirection });
        if (string.IsNullOrWhiteSpace(query.SearchTerm)) return result;
        var term = query.SearchTerm.Trim();
        return result.Where(x => x.BatchNumber.Contains(term) || x.Product.Name.Contains(term) || x.Warehouse.Name.Contains(term));
    }


}
