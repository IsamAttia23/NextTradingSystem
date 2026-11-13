using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class SalesInvoiceRepository(NEXTDbContext context) : KeyRepository<SalesInvoice, Guid>(context), ISalesInvoiceRepository
{
    public Task<SalesInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => StorageErrors.ExecuteAsync(async () =>
        {
            var invoices = Context.SalesInvoices.AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Warehouse)
                .Include(x => x.Items);
            var result = await (from row in invoices
                                join user in Context.Users on row.UserId equals user.Id
                                where row.Id == id
                                select new
                                {
                                    Invoice = row,
                                    User = new UserAccount
                                    {
                                        Id = user.Id, FullName = user.FullName, UserName = user.UserName,
                                        Email = user.Email, PhoneNumber = user.PhoneNumber, IsActive = user.IsActive,
                                        CreatedOn = user.CreatedOn, UpdatedOn = user.UpdatedOn, BranchId = user.BranchId,
                                        AvatarUrl = user.AvatarUrl, DeletionReason = user.DeletionReason
                                    }
                                }).FirstOrDefaultAsync(cancellationToken);

            if (result is null)
            {
                return null;
            }

            var invoice = result.Invoice;
            invoice.User = result.User;
            var productIds = invoice.Items.Select(x => x.ProductId).Distinct().ToArray();
            var products = await ReadProductsAsync(productIds, cancellationToken);
            AttachProducts(invoice, products);
            return invoice;
        });

    private Task<Dictionary<Guid, Product>> ReadProductsAsync(Guid[] productIds, CancellationToken ct)
    {
        if (productIds.Length == 0)
        {
            return Task.FromResult(new Dictionary<Guid, Product>());
        }

        // Historical invoice lines must retain products hidden by soft deletion.
        return Context.Products.AsNoTracking()
            .IgnoreQueryFilters([NEXTDbContext.SoftDeleteFilterName])
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }

    private static void AttachProducts(SalesInvoice invoice, IReadOnlyDictionary<Guid, Product> products)
    {
        foreach (var item in invoice.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
            {
                item.Product = product;
            }
        }
    }

    public override Expression<Func<SalesInvoice, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as SalesInvoiceQueryModel ?? new SalesInvoiceQueryModel
        {
            SearchTerm = query.SearchTerm
        });
    }

    public Expression<Func<SalesInvoice, bool>> BuildFilterExpression(SalesInvoiceQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var customerName = query.CustomerName?.Trim();
        Expression<Func<SalesInvoice, bool>> filters = entity =>
            (!query.From.HasValue || entity.InvoiceDate >= query.From.Value) &&
            (!query.To.HasValue || entity.InvoiceDate <= query.To.Value) &&
            (!query.CustomerId.HasValue || entity.CustomerId == query.CustomerId.Value) &&
            (string.IsNullOrEmpty(customerName) || (entity.Customer != null && entity.Customer.Name.Contains(customerName))) &&
            (!query.Status.HasValue || entity.Status == query.Status.Value);
        return CombineSearchExpression(filters, query.SearchTerm, "InvoiceNumber", "Notes");
    }

    public override IQueryable<SalesInvoice> ApplySorting(IQueryable<SalesInvoice> source, string? sortBy, SortDirection? sortDirection)
    {
        var descending = sortDirection == SortDirection.Descending;
        return ThenByField(OrderByField(source, "InvoiceDate", descending), "Id", descending);
    }
}
