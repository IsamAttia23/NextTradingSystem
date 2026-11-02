using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class CustomerRepository(NEXTDbContext context) : KeyRepository<Customer, Guid>(context), ICustomerRepository
{
    public override Expression<Func<Customer, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as CustomerQueryModel ?? new CustomerQueryModel { SearchTerm = query.SearchTerm });
    }

    public Expression<Func<Customer, bool>> BuildFilterExpression(CustomerQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var taxNumber = query.TaxNumber?.Trim();
        var commercialRegister = query.CommercialRegister?.Trim();
        Expression<Func<Customer, bool>> filters = entity =>
            (!query.Type.HasValue || entity.Type == query.Type.Value) &&
            (!query.Category.HasValue || entity.Category == query.Category.Value) &&
            (string.IsNullOrEmpty(taxNumber) || (entity.TaxNumber != null && entity.TaxNumber.Contains(taxNumber))) &&
            (string.IsNullOrEmpty(commercialRegister) || (entity.CommercialRegister != null && entity.CommercialRegister.Contains(commercialRegister))) &&
            (!query.IsActive.HasValue || entity.IsActive == query.IsActive.Value) &&
            (!query.TagId.HasValue || entity.TagId == query.TagId.Value) &&
            (!query.MinCreditLimit.HasValue || entity.CreditLimit >= query.MinCreditLimit.Value) &&
            (!query.MaxCreditLimit.HasValue || entity.CreditLimit <= query.MaxCreditLimit.Value) &&
            (!query.MinCurrentBalance.HasValue || entity.CurrentBalance >= query.MinCurrentBalance.Value) &&
            (!query.MaxCurrentBalance.HasValue || entity.CurrentBalance <= query.MaxCurrentBalance.Value) &&
            (!query.MinPaymentTermsDays.HasValue || entity.PaymentTermsDays >= query.MinPaymentTermsDays.Value) &&
            (!query.MaxPaymentTermsDays.HasValue || entity.PaymentTermsDays <= query.MaxPaymentTermsDays.Value);

        return CombineSearchExpression(filters, query.SearchTerm, "Name", "CompanyName", "Phone", "Email");
    }

    public override IQueryable<Customer> ApplySorting(IQueryable<Customer> customers, string? sortBy, SortDirection? sortDirection)
    {
        sortBy = new CustomerQueryModel().GetSortFields()
            .FirstOrDefault(field => string.Equals(field, sortBy?.Trim(), StringComparison.OrdinalIgnoreCase));
        var descending = sortDirection == SortDirection.Descending;

        var ordered = sortBy?.Trim().ToLowerInvariant() switch
        {
            "name" => descending
                ? customers.OrderByDescending(c => c.Name)
                : customers.OrderBy(c => c.Name),

            "companyname" => descending
                ? customers.OrderByDescending(c => c.CompanyName)
                : customers.OrderBy(c => c.CompanyName),

            "creditlimit" => descending
                ? customers.OrderByDescending(c => c.CreditLimit)
                : customers.OrderBy(c => c.CreditLimit),

            "currentbalance" => descending
                ? customers.OrderByDescending(c => c.CurrentBalance)
                : customers.OrderBy(c => c.CurrentBalance),

            "paymenttermsdays" => descending
                ? customers.OrderByDescending(c => c.PaymentTermsDays)
                : customers.OrderBy(c => c.PaymentTermsDays),

            _ => customers.OrderBy(c => c.Name)
        };
        return ordered.ThenBy(entity => entity.Id);
    }
}
