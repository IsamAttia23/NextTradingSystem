using System.Security.Cryptography;
using System.Text.Json;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Invoices;
using NEXT.Application.Validation;
using NEXT.Domain.Enums;
namespace NEXT.Application.Features.Accounting;

public sealed class DebtBookWithdrawals(DebtBookReads reads, IInvoiceConfirmationStore invoices, IInvoiceAccountingStore customers, IAccountingStore ledger,
 InvoiceCreation creation, InvoiceConfirmation confirmation, InvoiceFulfillment fulfillment, IUnitOfWork work, ICurrentUserService current, IUserQueryService users)
{
    public Task<Guid> WithdrawAsync(Guid accountId, DebtBookWithdrawalInput input, CancellationToken ct) => work.ExecuteReadCommittedAsync(async token =>
    {
        RequestValidation.Identifier(input.OperationId);
        var a = await reads.AccountAsync(accountId, token);
        if (input.CustomerId != a.CustomerId || input.RowVersion is not { Length: > 0 })
            RequestValidation.Fail("CustomerId", "AccountingChooseInvoiceCustomer");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { accountId, input.Lines, input.Notes, input.CustomerPhone, input.DiscountType, input.DiscountValue })));
        var actor = await users.FindUserAsync(current.UserId?.ToString() ?? "", token);
        if (actor is null || !actor.IsActive)
            throw new ConflictException("InvoiceActorUnavailable");
        await ledger.LockPostingAsync(input.OperationId, "debt-withdrawal:" + input.OperationId.ToString("N"), token);
        var existing = await invoices.LockInvoiceAsync(input.OperationId, token);
        if (existing is not null)
        {
            if (existing.UserId != actor.Id || existing.CustomerId != a.CustomerId || existing.DebtWithdrawalHash != hash || existing.Status is not (InvoiceStatus.Shipped or InvoiceStatus.Delivered))
                throw new ConflictException("AccountingOperationConflict");
            return existing.Id;
        }
        var result = await creation.CreateAsync(new CreateInvoiceInput { OperationId = input.OperationId, CustomerId = a.CustomerId, CustomerName = a.Name, Lines = input.Lines, Notes = input.Notes, DiscountType = input.DiscountType, DiscountValue = input.DiscountValue }, token);
        var invoice = await invoices.LockInvoiceAsync(result.Id, token) ?? throw new NotFoundException("InvoiceNotFound");
        await customers.LockCustomerAsync(a.CustomerId, token);
        var account = await customers.LockDebtAccountByIdAsync(accountId, token) ?? throw new NotFoundException("DebtBookAccountNotFound");
        if (!account.RowVersion.SequenceEqual(input.RowVersion))
            throw new ConflictException("DebtBookAccountStale");
        invoice.DebtWithdrawalHash = hash;
        await work.SaveChangesAsync(token);
        await confirmation.ConfirmAsync(invoice.Id, new() { OperationId = Guid.NewGuid(), CustomerId = a.CustomerId, RowVersion = invoice.RowVersion, Amount = 0 }, token);
        await fulfillment.ExecuteAsync(invoice.Id, new() { OperationId = Guid.NewGuid(), CustomerId = a.CustomerId, RowVersion = invoice.RowVersion, TargetStatus = InvoiceStatus.Delivered, CustomerPhone = input.CustomerPhone }, token);
        return invoice.Id;
    }, ct);
}
