using System.ComponentModel.DataAnnotations;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Accounting;

// Both debts and refundable customer balances must belong to an identified customer.
public sealed class InvoiceCustomerResolver(IInvoiceAccountingStore store, IRepository<Customer> customers)
{
    public async Task<Customer> ResolveAsync(Invoice invoice, Guid? selectedCustomerId, string? suppliedPhone,
        bool requirePhone, CancellationToken ct)
    {
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
        if (requirePhone && string.IsNullOrWhiteSpace(customer.Phone))
        {
            if (string.IsNullOrWhiteSpace(suppliedPhone)) RequestValidation.Fail("CustomerPhone", "AccountingCustomerPhoneRequired");
            var phone = RequestValidation.Text(suppliedPhone, "CustomerPhone", 32);
            if (!new PhoneAttribute().IsValid(phone)) RequestValidation.Fail("CustomerPhone", "AccountingCustomerPhoneRequired");
            customer.Phone = phone;
        }
        invoice.CustomerId = customer.Id;
        return customer;
    }
}
