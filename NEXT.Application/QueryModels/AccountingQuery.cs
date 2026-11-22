using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class AccountingQuery : PaginationQuery
{
    public AccountType? Account { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? PaymentId { get; set; }
}
