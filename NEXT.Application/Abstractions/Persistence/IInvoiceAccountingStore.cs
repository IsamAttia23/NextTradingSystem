using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Persistence;

public interface IInvoiceAccountingStore
{
    Task LockCustomerNameAsync(string normalizedName, CancellationToken ct);
    Task<Customer?> LockCustomerAsync(Guid id, CancellationToken ct);
    Task<CustomerDebtAccount?> LockDebtAccountAsync(Guid customerId, CancellationToken ct);
}
