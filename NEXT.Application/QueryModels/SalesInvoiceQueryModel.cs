using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class SalesInvoiceQueryModel : PaginationQuery
{
    public string? CustomerName { get; set; }
    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public Guid? CustomerId { get; set; }

    public InvoiceStatus? Status { get; set; }
}
