using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Accounting;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Application.Features.Invoices;

public sealed class InvoiceCancellation(IInvoiceConfirmationStore store, IInvoiceRepository invoices,
    IRepository<InvoiceStatusHistory> history, IReadRepository<Payment> payments, IQueryExecutor executor,
    IAccountingStore ledger, IAccountingPostingService posting, InvoiceCustomerResolver customers,
    ICurrentUserService currentUser, IUserQueryService users, IUnitOfWork unitOfWork, IInvoiceStateMachine states, TimeProvider clock)
{
    public Task<Guid> CancelAsync(Guid id, CancelInvoiceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(id);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        if (input.RowVersion is not { Length: > 0 }) RequestValidation.Fail(nameof(input.RowVersion), "InvoiceCancellationConflict");
        var reason = RequestValidation.Text(input.Reason, nameof(input.Reason), 500);
        var hash = InvoiceOperationFingerprint.Of(new { Reason = reason, input.CustomerId });
        return unitOfWork.ExecuteReadCommittedAsync(async token =>
        {
            var actor = await users.FindUserAsync(currentUser.UserId?.ToString() ?? "", token);
            if (actor is null || !actor.IsActive) RequestValidation.Fail("UserId", "InvoiceActorUnavailable");
            var invoice = await store.LockInvoiceAsync(id, token) ?? throw new NotFoundException("InvoiceNotFound");
            var previous = await history.FirstOrDefaultAsync(item => item.InvoiceId == id && item.OperationId == input.OperationId, token);
            if (previous is not null)
            {
                if (previous.ToStatus != InvoiceStatus.Cancelled || previous.ActorId != actor.Id || previous.RequestHash != hash)
                    throw new ConflictException("InvoiceCancellationConflict");
                return id;
            }
            if (await payments.AnyAsync(item => item.Id == input.OperationId, token)) throw new ConflictException("InvoiceCancellationConflict");
            if (invoice.Type != InvoiceType.Sales || invoice.LegacyInvoiceTypeCode is not null || invoice.ParentInvoiceId is not null ||
                !states.CanTransition(invoice.Status, InvoiceStatus.Cancelled)) throw new ConflictException("InvoiceCancellationUnavailable");
            invoices.SetRowVersion(invoice, input.RowVersion);
            var receipts = await executor.ToListAsync(payments.Query(item => item.InvoiceId == id), token);
            if (receipts.Any(item => item.PaymentType != PaymentType.Receipt || item.Amount <= 0) ||
                receipts.Sum(item => item.Amount) != invoice.PaidAmount || invoice.RemainingAmount != invoice.TotalAmount - invoice.PaidAmount ||
                invoice.PaidAmount < 0 || invoice.RemainingAmount < 0 ||
                await ledger.FindSourceAsync(LedgerEntry.Key(LedgerEntryKind.SalesRecognition, AccountingReferenceType.Invoice, id), token) is not null)
                throw new ConflictException("AccountingInvoiceReviewRequired");
            // Never transfer an advance which was not posted, or has already been reversed.
            foreach (var receipt in receipts)
            {
                var entry = await ledger.FindSourceAsync(LedgerEntry.Key(LedgerEntryKind.Receipt, AccountingReferenceType.Payment, receipt.Id), token);
                if (entry?.PostedAt is null || entry.InvoiceId != id || await ledger.HasReversalAsync(entry.Id, token) ||
                    entry.Lines.Where(line => line.AccountId == Account.IdFor(AccountType.CustomerAdvances)).Sum(line => line.Credit - line.Debit) != receipt.Amount)
                    throw new ConflictException("AccountingInvoiceReviewRequired");
            }
            if (invoice.PaidAmount > 0) await customers.ResolveAsync(invoice, input.CustomerId, null, false, token);
            var now = clock.GetUtcNow();
            if (invoice.Status == InvoiceStatus.Confirmed)
            {
                if (invoice.Items.Count == 0 || invoice.Items.Any(item => item.Quantity <= 0)) throw new ConflictException("InvoiceLinesInvalid");
                var quantities = invoice.Items.GroupBy(item => item.ProductId).ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
                var stocks = await store.LockStocksAsync(invoice.WarehouseId, quantities.Keys.Order().ToArray(), token);
                if (stocks.Count != quantities.Count || stocks.Any(stock => stock.ReservedQuantity < quantities[stock.ProductId]))
                    throw new ConflictException("InvoiceReservedStockInsufficient");
                foreach (var stock in stocks) { stock.ReservedQuantity -= quantities[stock.ProductId]; stock.LastUpdated = now; }
            }
            await history.AddAsync(new InvoiceStatusHistory { InvoiceId = id, OperationId = input.OperationId,
                FromStatus = invoice.Status, ToStatus = InvoiceStatus.Cancelled, ActionAt = now, ActorId = actor.Id,
                ActorFullName = RequestValidation.Text(actor.FullName, "ActorFullName", 200), Reason = reason, RequestHash = hash }, token);
            invoice.Status = states.Transition(invoice.Status, InvoiceStatus.Cancelled, reason);
            invoice.RemainingAmount = 0; invoice.LastActionAt = now;
            if (invoice.PaidAmount > 0)
                await posting.PostAsync(new(input.OperationId, LedgerEntryKind.InvoiceCancellation, AccountingReferenceType.Invoice, id,
                    DateOnly.FromDateTime(clock.GetLocalNow().DateTime), AccountingDescriptions.InvoiceCancellation,
                    [new(AccountType.CustomerAdvances, invoice.PaidAmount, 0, invoice.CustomerId),
                     new(AccountType.CustomerRefunds, 0, invoice.PaidAmount, invoice.CustomerId)], id), token);
            return id;
        }, ct);
    }
}
