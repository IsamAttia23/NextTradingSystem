using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;

public sealed class DebtBookQuery : PaginationQuery
{
    public bool? IsClosed { get; set; }
    public bool? HasBalance { get; set; }
    public override IReadOnlyList<string> GetSortFields() => ["Name", "Balance", "LastActivity"];
}
