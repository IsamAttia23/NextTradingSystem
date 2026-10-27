using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class SupplierQueryModel : PaginationQuery
{
    public bool? IsActive { get; set; }
}
