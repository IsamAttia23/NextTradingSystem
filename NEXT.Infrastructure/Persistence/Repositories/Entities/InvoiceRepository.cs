using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class InvoiceRepository(NEXTDbContext context) : KeyRepository<Invoice, Guid>(context), IInvoiceRepository
{
    public Task<Invoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => StorageErrors.ExecuteAsync(async () =>
        {
            var invoices = Context.Invoices.AsNoTracking()
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
            var productIds = invoice.Items.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToArray();
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

    private static void AttachProducts(Invoice invoice, IReadOnlyDictionary<Guid, Product> products)
    {
        foreach (var item in invoice.Items)
        {
            if (item.ProductId is { } id && products.TryGetValue(id, out var product))
            {
                item.Product = product;
            }
        }
    }

    public override Expression<Func<Invoice, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as InvoiceQueryModel ?? new InvoiceQueryModel
        {
            SearchTerm = query.SearchTerm
        });
    }

    public Expression<Func<Invoice, bool>> BuildFilterExpression(InvoiceQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Expression<Func<Invoice, bool>> filters = entity =>
            (!query.From.HasValue || entity.InvoiceDate >= query.From.Value) &&
            (!query.To.HasValue || entity.InvoiceDate <= query.To.Value) &&
            (!query.CustomerId.HasValue || entity.CustomerId == query.CustomerId.Value) &&
            (!query.Status.HasValue || entity.Status == query.Status.Value);
        return CombineSearchExpression(filters, query.SearchTerm, "InvoiceNumber", "Notes", "CustomerName", "Customer.Name");
    }

    public override IQueryable<Invoice> ApplySorting(IQueryable<Invoice> source, string? sortBy, SortDirection? sortDirection)
    {
        var descending = sortDirection == SortDirection.Descending;
        return ThenByField(OrderByField(source, "InvoiceDate", descending), "Id", descending);
    }
}
