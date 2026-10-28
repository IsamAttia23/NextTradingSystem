using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;
public sealed class CustomerContactQueryModel : PaginationQuery
{
    public Guid? CustomerId { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
