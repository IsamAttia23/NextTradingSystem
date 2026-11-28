using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Accounting;

// Opens the invoice's subledger item; manual debt management is a separate use case.
public sealed class InvoiceDebtResolver(IInvoiceAccountingStore store,
    IRepository<CustomerDebtAccount> accounts, IRepository<DebtItem> items, IUnitOfWork unitOfWork, TimeProvider clock, InvoiceCustomerResolver customerResolver)
{
    public async Task<DebtItem> ResolveAsync(Invoice invoice, Guid actorId, Guid? selectedCustomerId,
        string? suppliedPhone, DateOnly date, CancellationToken ct)
    {
        if (await items.AnyAsync(item => item.InvoiceId == invoice.Id, ct))
            throw new ConflictException("AccountingInvoiceAlreadyRecognized");
        var customer = await customerResolver.ResolveAsync(invoice, selectedCustomerId, suppliedPhone, true, ct);
        var account = await EnsureAccountAsync(customer.Id, actorId, ct);
        invoice.DebtAccountId = account.Id;
        var item = DebtItem.Create(account.Id, DebtItemKind.InvoiceBalance, invoice.Id, invoice.RemainingAmount,
            date, "متبقي فاتورة رقم " + invoice.InvoiceNumber, actorId);
        await items.AddAsync(item, ct);
        // The posting validator locks the new item. Both this save and the posting remain in the invoice transaction.
        await unitOfWork.SaveChangesAsync(ct);
        return item;
    }
    public async Task<CustomerDebtAccount> EnsureAsync(Invoice invoice, Guid actorId, Guid? customerId, CancellationToken ct)
    {
        var customer = await customerResolver.ResolveAsync(invoice, customerId, null, false, ct);
        var account = await EnsureAccountAsync(customer.Id, actorId, ct);
        invoice.DebtAccountId = account.Id;
        return account;
    }
    private async Task<CustomerDebtAccount> EnsureAccountAsync(Guid customerId, Guid actorId, CancellationToken ct)
    {
        await store.LockCustomerAsync(customerId, ct);
        var account = await store.LockDebtAccountAsync(customerId, ct);
        if (account is null)
        {
            account = CustomerDebtAccount.Create(customerId, actorId, clock.GetUtcNow(), await store.NextDebtCycleAsync(customerId, ct));
            await accounts.AddAsync(account, ct);
        }
        else
            account.RecordActivity(clock.GetUtcNow());
        return account;
    }
}
