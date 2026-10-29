using NEXT.Application.Common.QueryModels;

using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>
/// EF Core repository for Customer.
/// </summary>
public sealed class CustomerRepository(
    NEXTDbContext context)
    : KeyRepository<Customer, Guid>(context), ICustomerRepository
{
    public override IQueryable<Customer> Search(PaginationQuery query) =>
        Search(query as CustomerQueryModel ?? new CustomerQueryModel { SearchTerm = query.SearchTerm, SortBy = query.SortBy,
            SortDirection = query.SortDirection, PageNumber = query.PageNumber, PageSize = query.PageSize });

    public IQueryable<Customer> Search(
        CustomerQueryModel customerQuery)
    {
        ArgumentNullException.ThrowIfNull(customerQuery);

        var customers = Query(asNoTracking: true);

        customers = ApplySearchTerm(
            customers, customerQuery.SearchTerm);

        customers = ApplyFilters(
            customers, customerQuery);

        customers = ApplySorting(customers, customerQuery.SortBy, customerQuery.SortDirection);

        return customers;
    }

    // General search
    private static IQueryable<Customer> ApplySearchTerm(IQueryable<Customer> customers, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return customers;

        searchTerm = searchTerm.Trim();

        return customers.Where(c =>
            c.Name.Contains(searchTerm) ||
            (c.CompanyName != null &&
             c.CompanyName.Contains(searchTerm)) ||
            (c.Phone != null &&
             c.Phone.Contains(searchTerm)) ||
            (c.Email != null &&
             c.Email.Contains(searchTerm)));
    }

    // Optional filters
    private static IQueryable<Customer> ApplyFilters(IQueryable<Customer> customers, CustomerQueryModel query)
    {
        customers = ApplyCustomerTypeFilter(
            customers, query.Type);

        customers = ApplyCategoryFilter(
            customers, query.Category);


        customers = ApplyTaxNumberFilter(
            customers, query.TaxNumber);

        customers = ApplyCommercialRegisterFilter(
            customers, query.CommercialRegister);

        customers = ApplyActiveFilter(
            customers, query.IsActive);

        customers = ApplyTagFilter(
            customers, query.TagId);

        customers = ApplyCreditLimitFilter(
            customers, query.MinCreditLimit,
            query.MaxCreditLimit);

        customers = ApplyCurrentBalanceFilter(
            customers, query.MinCurrentBalance,
            query.MaxCurrentBalance);

        customers = ApplyPaymentTermsFilter(
            customers, query.MinPaymentTermsDays,
            query.MaxPaymentTermsDays);

        return customers;
    }

    private static IQueryable<Customer> ApplyCustomerTypeFilter(IQueryable<Customer> customers, CustomerType? type)
    {
        return type.HasValue
            ? customers.Where(c => c.Type == type.Value)
            : customers;
    }

    private static IQueryable<Customer> ApplyCategoryFilter(IQueryable<Customer> customers, CustomerCategory? category)
    {
        return category.HasValue
            ? customers.Where(c => c.Category == category.Value)
            : customers;
    }

    private static IQueryable<Customer> ApplyTaxNumberFilter(IQueryable<Customer> customers, string? taxNumber)
    {
        if (string.IsNullOrWhiteSpace(taxNumber))
            return customers;

        taxNumber = taxNumber.Trim();

        return customers.Where(c =>
            c.TaxNumber != null &&
            c.TaxNumber.Contains(taxNumber));
    }

    private static IQueryable<Customer> ApplyCommercialRegisterFilter(IQueryable<Customer> customers, string? commercialRegister)
    {
        if (string.IsNullOrWhiteSpace(commercialRegister))
            return customers;

        commercialRegister = commercialRegister.Trim();

        return customers.Where(c =>
            c.CommercialRegister != null &&
            c.CommercialRegister.Contains(commercialRegister));
    }

    private static IQueryable<Customer> ApplyActiveFilter(IQueryable<Customer> customers, bool? isActive)
    {
        return isActive.HasValue
            ? customers.Where(c => c.IsActive == isActive.Value)
            : customers;
    }

    private static IQueryable<Customer> ApplyTagFilter(IQueryable<Customer> customers, Guid? tagId)
    {
        return tagId.HasValue
            ? customers.Where(c => c.TagId == tagId.Value)
            : customers;
    }

    private static IQueryable<Customer> ApplyCreditLimitFilter(IQueryable<Customer> customers, decimal? min, decimal? max)
    {
        if (min.HasValue)
            customers = customers.Where(c => c.CreditLimit >= min.Value);

        if (max.HasValue)
            customers = customers.Where(c => c.CreditLimit <= max.Value);

        return customers;
    }

    private static IQueryable<Customer> ApplyCurrentBalanceFilter(IQueryable<Customer> customers, decimal? min, decimal? max)
    {
        if (min.HasValue)
            customers = customers.Where(c => c.CurrentBalance >= min.Value);

        if (max.HasValue)
            customers = customers.Where(c => c.CurrentBalance <= max.Value);

        return customers;
    }

    private static IQueryable<Customer> ApplyPaymentTermsFilter(IQueryable<Customer> customers, int? min, int? max)
    {
        if (min.HasValue)
            customers = customers.Where(c =>
                c.PaymentTermsDays >= min.Value);

        if (max.HasValue)
            customers = customers.Where(c =>
                c.PaymentTermsDays <= max.Value);

        return customers;
    }

    // Sorting
    private static IQueryable<Customer> ApplySorting(IQueryable<Customer> customers, string? sortBy, SortDirection? direction)
    {
        var descending = direction == SortDirection.Descending;

        return sortBy?.Trim().ToLowerInvariant() switch
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
    }
}
