using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
using MediatR;
using NEXT.Application.Features.Invoices.Events;
using NEXT.Application.Features.Accounting;

namespace NEXT.Application.Features.Invoices;

public sealed class InvoiceConfirmation(IInvoiceConfirmationStore store, IInvoiceRepository invoices,
    IRepository<InvoiceStatusHistory> history, IRepository<Payment> payments, IReadRepository<Warehouse> warehouses,
    IReadRepository<Product> products, ICurrentUserService currentUser, IUserQueryService users,
    IInvoiceStateMachine stateMachine, IUnitOfWork unitOfWork, TimeProvider clock, IQueryExecutor executor, IPublisher publisher,
    InvoiceCustomerResolver customerResolver, InvoiceStockPolicy stockPolicy)
{
    public Task<Guid> ConfirmAsync(Guid id, ConfirmInvoiceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CashAccount is { } cashAccount && !NEXT.Domain.Services.CashAccounts.IsCash(cashAccount))
            RequestValidation.Fail("CashAccount", "CashAccountInvalid");
        RequestValidation.Identifier(id);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        if (input.Amount < 0 || input.Amount != decimal.Round(input.Amount, 2))
            RequestValidation.Fail(nameof(input.Amount), "InvoicePaymentInvalid");
        if (input.PaymentMethod is { } method && !Enum.IsDefined(method))
            RequestValidation.Fail(nameof(input.PaymentMethod), "InvoicePaymentMethodRequired");
        if (input.Amount > 0 && input.PaymentMethod is null)
            RequestValidation.Fail(nameof(input.PaymentMethod), "InvoicePaymentMethodRequired");
        var selectedMethod = input.Amount > 0 ? input.PaymentMethod : null;

        return unitOfWork.ExecuteReadCommittedAsync(async token =>
        {
            var actor = await users.FindUserAsync(currentUser.UserId?.ToString() ?? "", token);
            if (actor is null || !actor.IsActive)
                RequestValidation.Fail("UserId", "InvoiceActorUnavailable");
            var invoice = await store.LockInvoiceAsync(id, token) ?? throw new NotFoundException("InvoiceNotFound");
            var previous = await history.FirstOrDefaultAsync(item => item.InvoiceId == id && item.OperationId == input.OperationId, token);
            if (previous is not null)
            {
                if (previous.FromStatus != InvoiceStatus.Pending || previous.ToStatus != InvoiceStatus.Confirmed ||
                    previous.ActorId != actor.Id || previous.PaymentAmount != input.Amount || previous.PaymentMethod != selectedMethod ||
                    (input.CustomerId.HasValue && input.CustomerId != invoice.CustomerId))
                    throw new ConflictException("InvoiceConfirmationConflict");
                if (input.Amount > 0 && (await payments.FirstOrDefaultAsync(p => p.Id == input.OperationId, token))?.CashAccount != NEXT.Domain.Services.CashAccounts.Resolve(input.CashAccount, selectedMethod!.Value))
                    throw new ConflictException("InvoiceConfirmationConflict");
                return invoice.Id;
            }

            invoices.SetRowVersion(invoice, input.RowVersion);
            if (invoice.Type != InvoiceType.Sales || invoice.LegacyInvoiceTypeCode is not null ||
                invoice.ParentInvoiceId is not null || !stateMachine.CanTransition(invoice.Status, InvoiceStatus.Confirmed))
                throw new ConflictException("InvoiceTransitionInvalid");
            if (!await warehouses.AnyAsync(item => item.Id == invoice.WarehouseId && item.IsActive, token))
                RequestValidation.Fail("WarehouseId", "InvoiceWarehouseUnavailable");
            if (await payments.AnyAsync(item => item.Id == input.OperationId, token))
                throw new ConflictException("InvoiceConfirmationConflict");
            var recorded = await executor.ToListAsync(payments.Query(item => item.InvoiceId == id), token);
            if (recorded.Any(item => item.PaymentType != PaymentType.Receipt || item.Amount <= 0) ||
                recorded.Sum(item => item.Amount) != invoice.PaidAmount ||
                invoice.RemainingAmount != invoice.TotalAmount - invoice.PaidAmount)
                throw new ConflictException("InvoiceExistingPayments");
            var paid = invoice.PaidAmount + input.Amount;
            PaymentStatus paymentStatus;
            try
            {
                paymentStatus = InvoicePaymentRules.Status(invoice.TotalAmount, paid);
            }
            catch (ArgumentOutOfRangeException)
            {
                RequestValidation.Fail(nameof(input.Amount), "InvoicePaymentInvalid");
                throw;
            }

            var quantities = RequiredQuantities(invoice);
            await customerResolver.ResolveAsync(invoice, input.CustomerId, null, false, token);
            var productIds = quantities.Keys.Order().ToArray();
            if (await products.CountAsync(item => productIds.Contains(item.Id) && item.IsActive, token) != quantities.Count)
                RequestValidation.Fail("Lines", "InvoiceProductUnavailable");
            if (stockPolicy.AllowOverselling) await store.EnsureStocksAsync(invoice.WarehouseId, productIds, token);
            var stocks = await store.LockStocksAsync(invoice.WarehouseId, productIds, token);
            if (stocks.Count != quantities.Count || (!stockPolicy.AllowOverselling && stocks.Any(stock => stock.Quantity - stock.ReservedQuantity < quantities[stock.ProductId])))
                RequestValidation.Fail("Lines", "InvoiceInsufficientStock");
            var now = clock.GetUtcNow();
            foreach (var stock in stocks)
            {
                stock.ReservedQuantity += quantities[stock.ProductId];
                stock.LastUpdated = now;
            }

            Payment? payment = null;
            if (input.Amount > 0)
            {
                payment = new Payment
                {
                    Id = input.OperationId,
                    CreatedBy = actor.Id,
                    PaymentNumber = "PAY-" + input.OperationId.ToString("N"),
                    RecordedByFullName = RequestValidation.Text(actor.FullName, "RecordedByFullName", 200),
                    PaymentType = PaymentType.Receipt,
                    Purpose = PaymentPurpose.InvoiceReceipt,
                    InvoiceId = invoice.Id,
                    CustomerId = invoice.CustomerId,
                    Amount = input.Amount,
                    PaymentMethod = selectedMethod!.Value,
                    CashAccount = NEXT.Domain.Services.CashAccounts.Resolve(input.CashAccount, selectedMethod.Value),
                    PaymentDate = DateOnly.FromDateTime(clock.GetLocalNow().DateTime)
                };
                await payments.AddAsync(payment, token);
            }

            await history.AddAsync(new InvoiceStatusHistory
            {
                InvoiceId = id, OperationId = input.OperationId,
                FromStatus = invoice.Status, ToStatus = InvoiceStatus.Confirmed,
                ActionAt = now, ActorId = actor.Id,
                ActorFullName = RequestValidation.Text(actor.FullName, "ActorFullName", 200),
                PaymentAmount = input.Amount, PaymentMethod = selectedMethod
            }, token);
            invoice.Status = stateMachine.Transition(invoice.Status, InvoiceStatus.Confirmed);
            invoice.LastActionAt = now;
            invoice.PaidAmount = paid;
            invoice.RemainingAmount = invoice.TotalAmount - paid;
            invoice.PaymentStatus = paymentStatus;
            if (selectedMethod is not null) invoice.PaymentMethod = selectedMethod;
            if (payment is not null) await publisher.Publish(new InvoicePaymentRecorded(invoice, payment), token);
            return id;
        }, ct);
    }

    private static Dictionary<Guid, decimal> RequiredQuantities(Invoice invoice)
    {
        if (invoice.Items.Count == 0 || invoice.Items.Any(item => !InvoiceStockLines.IsValid(item) ||
            item.Quantity <= 0 || item.Quantity > InvoiceAmounts.MaximumQuantity || decimal.Round(item.Quantity, 3) != item.Quantity))
            RequestValidation.Fail("Lines", "InvoiceLinesInvalid");
        return invoice.Items.Where(item => item.ProductId.HasValue).GroupBy(item => item.ProductId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
    }
}
