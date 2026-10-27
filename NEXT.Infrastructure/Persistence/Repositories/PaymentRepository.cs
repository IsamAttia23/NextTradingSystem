using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Payment; inherits common CRUD behavior from the generic repository.</summary>
public sealed class PaymentRepository(NEXTDbContext context) : KeyRepository<Payment, Guid>(context), IPaymentRepository
{
    public IQueryable<Payment> SearchAsync(PaymentQueryModel queryModel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryModel);
        var payments = Context.Payments.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Supplier)
            .AsQueryable();
        if (queryModel.From.HasValue) payments = payments.Where(x => x.PaymentDate >= queryModel.From);
        if (queryModel.To.HasValue) payments = payments.Where(x => x.PaymentDate <= queryModel.To);
        if (!string.IsNullOrEmpty(queryModel.CustomerName)) payments = payments.Where(x => x.Customer != null && x.Customer.Name == queryModel.CustomerName);
        if (!string.IsNullOrEmpty(queryModel.SupplierName)) payments = payments.Where(x => x.Supplier != null && x.Supplier.Name == queryModel.SupplierName);

        payments = queryModel.SortDirection switch
        {
            SortDirection.Ascending => payments
                .OrderBy(x => x.PaymentDate)
                .ThenBy(x => x.Id),

            SortDirection.Descending => payments
                .OrderByDescending(x => x.PaymentDate)
                .ThenByDescending(x => x.Id),

            _ => payments
                .OrderByDescending(x => x.PaymentDate)
                .ThenByDescending(x => x.Id)
        };

        return payments;
    }
}
