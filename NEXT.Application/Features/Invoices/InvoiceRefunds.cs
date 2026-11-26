using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Invoices;

public sealed class InvoiceRefunds(IInvoiceConfirmationStore store, IInvoiceRepository invoices, IRepository<Payment> payments,
    IReadRepository<InvoiceStatusHistory> history, IReadRepository<AccountTransaction> movements, IQueryExecutor executor,
    IAccountingStore ledger, IAccountingPostingService posting, ICurrentUserService currentUser,
    IUserQueryService users, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public Task<Guid> RefundAsync(Guid id, RecordInvoicePaymentInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CashAccount is { } cashAccount && !NEXT.Domain.Services.CashAccounts.IsCash(cashAccount))
            RequestValidation.Fail("CashAccount", "CashAccountInvalid");
        RequestValidation.Identifier(id);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        if (input.RowVersion is not { Length: > 0 }) RequestValidation.Fail(nameof(input.RowVersion), "InvoiceRefundConflict");
        if (input.Amount <= 0 || input.Amount != decimal.Round(input.Amount, 2)) RequestValidation.Fail(nameof(input.Amount), "InvoiceRefundInvalid");
        var method = input.PaymentMethod.GetValueOrDefault();
        if (input.PaymentMethod is null || !Enum.IsDefined(method)) RequestValidation.Fail(nameof(input.PaymentMethod), "InvoicePaymentMethodRequired");
        return unitOfWork.ExecuteReadCommittedAsync(async token =>
        {
            var actor = await users.FindUserAsync(currentUser.UserId?.ToString() ?? "", token);
            if (actor is null || !actor.IsActive) RequestValidation.Fail("UserId", "InvoiceActorUnavailable");
            var invoice = await store.LockInvoiceAsync(id, token) ?? throw new NotFoundException("InvoiceNotFound");
            var previous = await payments.FirstOrDefaultAsync(item => item.Id == input.OperationId, token);
            if (previous is not null)
            {
                if (previous.InvoiceId != id || previous.CreatedBy != actor.Id || previous.Purpose != PaymentPurpose.CustomerRefund ||
                    previous.PaymentType != PaymentType.Payment || previous.Amount != input.Amount || previous.CashAccount != NEXT.Domain.Services.CashAccounts.Resolve(input.CashAccount, method) || previous.PaymentMethod != method)
                    throw new ConflictException("InvoiceRefundConflict");
                return id;
            }
            if (await history.AnyAsync(item => item.InvoiceId == id && item.OperationId == input.OperationId, token))
                throw new ConflictException("InvoiceRefundConflict");
            if (invoice.Status != InvoiceStatus.Cancelled || invoice.Type != InvoiceType.Sales || invoice.CustomerId is null)
                throw new ConflictException("InvoiceRefundUnavailable");
            var cancellation = await ledger.FindSourceAsync(LedgerEntry.Key(LedgerEntryKind.InvoiceCancellation, AccountingReferenceType.Invoice, id), token);
            if (cancellation?.PostedAt is null || await ledger.HasReversalAsync(cancellation.Id, token))
                throw new ConflictException("AccountingInvoiceReviewRequired");
            var account = Account.IdFor(AccountType.CustomerRefunds);
            var amounts = await executor.ToListAsync(movements.Query(line => line.AccountId == account &&
                line.LedgerEntry.InvoiceId == id && line.LedgerEntry.PostedAt != null).Select(line => new { Amount = line.Credit - line.Debit }), token);
            var balance = amounts.Sum(item => item.Amount);
            if (input.Amount > balance) RequestValidation.Fail(nameof(input.Amount), "InvoiceRefundInvalid");
            invoices.SetRowVersion(invoice, input.RowVersion);
            var payment = new Payment { Id = input.OperationId, InvoiceId = id, CustomerId = invoice.CustomerId,
                PaymentNumber = "REF-" + input.OperationId.ToString("N"), PaymentType = PaymentType.Payment,
                Purpose = PaymentPurpose.CustomerRefund, Amount = input.Amount, PaymentMethod = method,
                CashAccount = NEXT.Domain.Services.CashAccounts.Resolve(input.CashAccount, method),
                PaymentDate = DateOnly.FromDateTime(clock.GetLocalNow().DateTime), CreatedBy = actor.Id,
                RecordedByFullName = RequestValidation.Text(actor.FullName, "RecordedByFullName", 200) };
            await payments.AddAsync(payment, token);
            invoice.LastActionAt = clock.GetUtcNow();
            await posting.PostAsync(new(input.OperationId, LedgerEntryKind.Refund, AccountingReferenceType.Payment, payment.Id,
                payment.PaymentDate, NEXT.Application.Features.Accounting.AccountingDescriptions.InvoiceRefund,
                [new(AccountType.CustomerRefunds, input.Amount, 0, invoice.CustomerId),
                 new(payment.CashAccount, 0, input.Amount, invoice.CustomerId)], id, payment.Id), token);
            return id;
        }, ct);
    }
}
