using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
using MediatR;
using NEXT.Application.Features.Invoices.Events;

namespace NEXT.Application.Features.Invoices;

public sealed class InvoiceFulfillment(IInvoiceConfirmationStore store, IInvoiceRepository invoices,
    IRepository<InvoiceStatusHistory> history, IReadRepository<Payment> payments, IInventoryService inventory,
    ICurrentUserService currentUser, IUserQueryService users, IInvoiceStateMachine stateMachine,
    IUnitOfWork unitOfWork, TimeProvider clock, IPublisher publisher)
{
    public Task<Guid> ExecuteAsync(Guid id, InvoiceFulfillmentInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(id);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        if (input.RowVersion is not { Length: > 0 })
            RequestValidation.Fail(nameof(input.RowVersion), "InvoiceFulfillmentConflict");
        if (input.TargetStatus is not (InvoiceStatus.Shipped or InvoiceStatus.Delivered))
            RequestValidation.Fail(nameof(input.TargetStatus), "InvoiceFulfillmentUnavailable");
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : RequestValidation.Text(input.Note, nameof(input.Note), 500);

        return unitOfWork.ExecuteReadCommittedAsync(async token =>
        {
            var actor = await users.FindUserAsync(currentUser.UserId?.ToString() ?? "", token);
            if (actor is null || !actor.IsActive)
                RequestValidation.Fail("UserId", "InvoiceActorUnavailable");
            var invoice = await store.LockInvoiceAsync(id, token) ?? throw new NotFoundException("InvoiceNotFound");
            var previous = await history.FirstOrDefaultAsync(item => item.InvoiceId == id && item.OperationId == input.OperationId, token);
            if (previous is not null)
            {
                if (previous.ActorId != actor.Id || previous.ToStatus != input.TargetStatus || previous.Reason != note ||
                    (input.CustomerId.HasValue && input.CustomerId != invoice.CustomerId))
                    throw new ConflictException("InvoiceFulfillmentConflict");
                return id;
            }
            if (await payments.AnyAsync(item => item.Id == input.OperationId, token))
                throw new ConflictException("InvoiceFulfillmentConflict");
            invoices.SetRowVersion(invoice, input.RowVersion);
            if (invoice.Type != InvoiceType.Sales || invoice.LegacyInvoiceTypeCode is not null || invoice.ParentInvoiceId is not null ||
                !stateMachine.CanTransition(invoice.Status, input.TargetStatus))
                throw new ConflictException("InvoiceFulfillmentUnavailable");
            var now = clock.GetUtcNow();
            if (invoice.Status == InvoiceStatus.Confirmed)
            {
                if (invoice.Items.Count == 0 || invoice.Items.Any(item => !InvoiceStockLines.IsValid(item) || item.Quantity <= 0 ||
                    item.Quantity > InvoiceAmounts.MaximumQuantity || decimal.Round(item.Quantity, 3) != item.Quantity))
                    RequestValidation.Fail("Lines", "InvoiceLinesInvalid");
                var quantities = invoice.Items.Where(item => item.ProductId.HasValue).GroupBy(item => item.ProductId!.Value)
                    .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
                if (quantities.Count > 0) await inventory.IssueReservedInvoiceAsync(new InvoiceDispatchInput(id, invoice.WarehouseId, quantities, now), token);
            }
            await history.AddAsync(new InvoiceStatusHistory
            {
                InvoiceId = id, OperationId = input.OperationId, FromStatus = invoice.Status, ToStatus = input.TargetStatus,
                ActionAt = now, ActorId = actor.Id, ActorFullName = RequestValidation.Text(actor.FullName, "ActorFullName", 200),
                Reason = note
            }, token);
            var from = invoice.Status;
            invoice.Status = stateMachine.Transition(from, input.TargetStatus);
            invoice.LastActionAt = now;
            await publisher.Publish(new InvoiceStatusChanged(invoice, from, invoice.Status, input.OperationId, actor.Id,
                input.CustomerId, input.CustomerPhone, DateOnly.FromDateTime(clock.GetLocalNow().DateTime)), token);
            return id;
        }, ct);
    }
}
