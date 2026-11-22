using System.ComponentModel.DataAnnotations;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Accounting;

// Opens the invoice's subledger item; manual debt management is a separate use case.
public sealed class InvoiceDebtResolver(IInvoiceAccountingStore store, IRepository<Customer> customers,
    IRepository<CustomerDebtAccount> accounts, IRepository<DebtItem> items, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<DebtItem> ResolveAsync(Invoice invoice, Guid actorId, Guid? selectedCustomerId,
        string? suppliedPhone, DateOnly date, CancellationToken ct)
    {
        if (await items.AnyAsync(item => item.InvoiceId == invoice.Id, ct))
            throw new ConflictException("AccountingInvoiceAlreadyRecognized");
        if (invoice.CustomerId.HasValue && selectedCustomerId.HasValue && invoice.CustomerId != selectedCustomerId)
            throw new ConflictException("AccountingChooseInvoiceCustomer");
        var customerId = invoice.CustomerId ?? selectedCustomerId;
        Customer customer;
        if (customerId.HasValue)
        {
            customer = await store.LockCustomerAsync(customerId.Value, ct) ?? throw new ConflictException("AccountingDebtCustomerInvalid");
            if (!customer.IsActive || customer.DeletedAt.HasValue) throw new ConflictException("AccountingDebtCustomerInvalid");
            if (invoice.CustomerId is null && !string.Equals(customer.Name.Trim(), invoice.CustomerName?.Trim(), StringComparison.OrdinalIgnoreCase))
                RequestValidation.Fail("CustomerId", "AccountingChooseInvoiceCustomer");
        }
        else
        {
            var name = RequestValidation.Text(invoice.CustomerName, "CustomerName", 200);
            await store.LockCustomerNameAsync(name.ToUpperInvariant(), ct);
            if (await customers.AnyAsync(item => item.IsActive && item.Name.ToUpper() == name.ToUpperInvariant(), ct))
                RequestValidation.Fail("CustomerId", "AccountingChooseInvoiceCustomer");
            customer = new Customer { Id = Guid.NewGuid(), Name = name };
            await customers.AddAsync(customer, ct);
        }
        if (string.IsNullOrWhiteSpace(customer.Phone))
        {
            if (string.IsNullOrWhiteSpace(suppliedPhone)) RequestValidation.Fail("CustomerPhone", "AccountingCustomerPhoneRequired");
            var phone = RequestValidation.Text(suppliedPhone, "CustomerPhone", 32);
            if (!new PhoneAttribute().IsValid(phone)) RequestValidation.Fail("CustomerPhone", "AccountingCustomerPhoneRequired");
            customer.Phone = phone;
        }
        invoice.CustomerId = customer.Id;
        var account = await store.LockDebtAccountAsync(customer.Id, ct);
        if (account is null)
        {
            account = CustomerDebtAccount.Create(customer.Id, actorId, clock.GetUtcNow());
            await accounts.AddAsync(account, ct);
        }
        else account.RecordActivity(clock.GetUtcNow());
        var item = DebtItem.Create(account.Id, DebtItemKind.InvoiceBalance, invoice.Id, invoice.RemainingAmount,
            date, "متبقي فاتورة رقم " + invoice.InvoiceNumber, actorId);
        await items.AddAsync(item, ct);
        // The posting validator locks the new item. Both this save and the posting remain in the invoice transaction.
        await unitOfWork.SaveChangesAsync(ct);
        return item;
    }
}
