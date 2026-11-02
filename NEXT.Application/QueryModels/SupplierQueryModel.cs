using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class SupplierQueryModel : PaginationQuery
{
    public override IReadOnlyList<string> GetSortFields() => ["Name", "Code"];

    public bool? IsActive { get; set; }
}
