using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class PaymentRepository(NEXTDbContext context) : KeyRepository<Payment, Guid>(context), IPaymentRepository
{
    public override Expression<Func<Payment, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as PaymentQueryModel ?? new PaymentQueryModel
        {
            SearchTerm = query.SearchTerm
        });
    }

    public Expression<Func<Payment, bool>> BuildFilterExpression(PaymentQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Expression<Func<Payment, bool>> filters = entity =>
            (!query.From.HasValue || entity.PaymentDate >= query.From.Value) &&
            (!query.To.HasValue || entity.PaymentDate <= query.To.Value) &&
            (string.IsNullOrEmpty(query.CustomerName) || (entity.Customer != null && entity.Customer.Name == query.CustomerName)) &&
            (string.IsNullOrEmpty(query.SupplierName) || (entity.Supplier != null && entity.Supplier.Name == query.SupplierName));
        return CombineSearchExpression(filters, query.SearchTerm, "PaymentNumber", "Notes");
    }

    public override IQueryable<Payment> ApplySorting(IQueryable<Payment> entities, string? sortBy, SortDirection? sortDirection)
        => base.ApplySorting(entities, string.IsNullOrWhiteSpace(sortBy) ? nameof(Payment.PaymentDate) : sortBy,
            sortDirection ?? SortDirection.Descending);
}
