using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class InvoiceQueryModel : PaginationQuery
{
    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public Guid? CustomerId { get; set; }

    public InvoiceStatus? Status { get; set; }
}
