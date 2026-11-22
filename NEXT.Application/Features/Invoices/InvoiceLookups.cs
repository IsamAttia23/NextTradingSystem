using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Features.Products;
using NEXT.Application.QueryModels;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Invoices;

public sealed class InvoiceLookups(IReadRepository<Customer> customers, IReadRepository<Product> products,
    IReadRepository<Warehouse> warehouses, IQueryExecutor executor, ProductPricing pricing)
{
    public async Task<Guid> DefaultWarehouseAsync(CancellationToken ct)
    {
        var warehouse = await warehouses.FirstOrDefaultAsync(item => item.IsDefault && item.IsActive, ct);
        if (warehouse is null)
            RequestValidation.Fail("WarehouseId", "حدّد مستودعًا افتراضيًا نشطًا في إعدادات المستودعات أولًا.");
        return warehouse.Id;
    }

    public Task<IReadOnlyList<InvoiceCustomerOption>> CustomersAsync(string? search, CancellationToken ct)
    {
        var term = SearchTerm(search);
        var filter = customers.BuildFilterExpression(new CustomerQueryModel { SearchTerm = term, IsActive = true });
        return executor.ToListAsync(customers.Query(filter).OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Take(12).Select(item => new InvoiceCustomerOption(item.Id, item.Name, item.Phone)), ct);
    }

    public async Task<IReadOnlyList<InvoiceProductOption>> ProductsAsync(string? search, CancellationToken ct)
    {
        var term = SearchTerm(search);
        var warehouse = await DefaultWarehouseAsync(ct);
        var filter = products.BuildFilterExpression(new ProductQueryModel { SearchTerm = term });
        var query = products.Query(filter).Where(item => item.IsActive)
            .OrderBy(item => item.Name).ThenBy(item => item.Id).Take(12);
        return await ReadProductsAsync(query, warehouse, ct);
    }

    internal Task<IReadOnlyList<InvoiceProductOption>> FindProductsAsync(Guid[] ids, Guid warehouse, CancellationToken ct) =>
        ReadProductsAsync(products.Query(item => item.IsActive && ids.Contains(item.Id)), warehouse, ct);

    private static string SearchTerm(string? search)
    {
        var term = search?.Trim() ?? "";
        if (term.Length > 200)
            RequestValidation.Fail("SearchTerm", "The supplied value is invalid.");
        return term;
    }

    internal async Task<InvoiceCustomerOption?> ResolveCustomerAsync(Guid? id, string name, CancellationToken ct)
    {
        if (id is { } selected)
        {
            var customer = await customers.FirstOrDefaultAsync(item => item.Id == selected && item.IsActive, ct);
            if (customer is null || !string.Equals(customer.Name, name, StringComparison.OrdinalIgnoreCase))
                RequestValidation.Fail("CustomerName", "أعد اختيار العميل من نتائج البحث.");
            return new(customer.Id, customer.Name, customer.Phone);
        }

        var normalized = name.ToUpperInvariant();
        var matches = await executor.ToListAsync(customers.Query(item => item.IsActive && item.Name.ToUpper() == normalized)
            .Take(2).Select(item => new InvoiceCustomerOption(item.Id, item.Name, item.Phone)), ct);
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<IReadOnlyList<InvoiceProductOption>> ReadProductsAsync(IQueryable<Product> query, Guid warehouse, CancellationToken ct)
    {
        var rate = await pricing.GetRateAsync(ct);
        var rows = await executor.ToListAsync(query.Select(item => new
        {
            item.Id, item.Name, item.Barcode, item.RetailPrice, item.CostPrice,
            Stock = item.Stocks.Where(stock => stock.WarehouseId == warehouse).Sum(stock => stock.Quantity - stock.ReservedQuantity)
        }), ct);
        return rows.Select(item => new InvoiceProductOption(item.Id, item.Name, item.Barcode,
            InvoiceAmounts.Money(item.CostPrice * rate * (1 + item.RetailPrice / 100m)),
            InvoiceAmounts.Money(item.CostPrice * rate), item.Stock)).ToArray();
    }
}
