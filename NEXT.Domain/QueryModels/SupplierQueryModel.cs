using NEXT.Domain.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Domain.QueryModels;

public sealed class SupplierQueryModel : PaginationQuery
{
    public bool? IsActive { get; set; }
}