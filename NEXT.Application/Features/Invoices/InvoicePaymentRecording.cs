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

namespace NEXT.Application.Features.Invoices;

public sealed class InvoicePaymentRecording(IInvoiceConfirmationStore store, IInvoiceRepository invoices,
    IRepository<Payment> payments, IReadRepository<InvoiceStatusHistory> history,
    ICurrentUserService currentUser, IUserQueryService users, IUnitOfWork unitOfWork,
    IQueryExecutor executor, TimeProvider clock, IPublisher publisher)
{
    public Task<Guid> RecordAsync(Guid id, RecordInvoicePaymentInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(id);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        if (input.RowVersion is not { Length: > 0 })
            RequestValidation.Fail(nameof(input.RowVersion), "InvoicePaymentConflict");
        if (input.Amount <= 0 || decimal.Round(input.Amount, 2) != input.Amount)
            RequestValidation.Fail(nameof(input.Amount), "InvoiceLaterPaymentInvalid");
        if (input.PaymentMethod is not { } method || !Enum.IsDefined(method))
            RequestValidation.Fail(nameof(input.PaymentMethod), "InvoicePaymentMethodRequired");

        return unitOfWork.ExecuteReadCommittedAsync(async token =>
        {
            var actor = await users.FindUserAsync(currentUser.UserId?.ToString() ?? "", token);
            if (actor is null || !actor.IsActive)
                RequestValidation.Fail("UserId", "InvoiceActorUnavailable");
            var invoice = await store.LockInvoiceAsync(id, token) ?? throw new NotFoundException("InvoiceNotFound");
            if (await history.AnyAsync(item => item.InvoiceId == id && item.OperationId == input.OperationId, token))
                throw new ConflictException("InvoicePaymentConflict");
            var previous = await payments.FirstOrDefaultAsync(item => item.Id == input.OperationId, token);
            if (previous is not null)
            {
                if (previous.InvoiceId != id || previous.CreatedBy != actor.Id ||
                    previous.PaymentType != PaymentType.Receipt || previous.Amount != input.Amount ||
                    previous.PaymentMethod != input.PaymentMethod)
                    throw new ConflictException("InvoicePaymentConflict");
                return id;
            }

            invoices.SetRowVersion(invoice, input.RowVersion);
            if (invoice.Type != InvoiceType.Sales || invoice.Status == InvoiceStatus.Cancelled || !InvoicePaymentRules.CanReceivePayment(invoice.RemainingAmount) ||
                invoice.LegacyInvoiceTypeCode is not null || invoice.ParentInvoiceId is not null ||
                invoice.PaidAmount < 0)
                throw new ConflictException("InvoicePaymentUnavailable");
            var recorded = await executor.ToListAsync(payments.Query(item => item.InvoiceId == id), token);
            if (recorded.Any(item => item.PaymentType != PaymentType.Receipt || item.Amount <= 0) ||
                recorded.Sum(item => item.Amount) != invoice.PaidAmount ||
                invoice.RemainingAmount != invoice.TotalAmount - invoice.PaidAmount)
                throw new ConflictException("InvoicePaymentConflict");
            if (input.Amount > invoice.RemainingAmount)
                RequestValidation.Fail(nameof(input.Amount), "InvoiceLaterPaymentInvalid");

            var paid = invoice.PaidAmount + input.Amount;
            var status = InvoicePaymentRules.Status(invoice.TotalAmount, paid);
            var payment = new Payment
            {
                Id = input.OperationId, PaymentNumber = "PAY-" + input.OperationId.ToString("N"),
                PaymentType = PaymentType.Receipt, Purpose = PaymentPurpose.InvoiceReceipt, InvoiceId = id, CustomerId = invoice.CustomerId,
                Amount = input.Amount, PaymentMethod = input.PaymentMethod!.Value,
                PaymentDate = DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
                CreatedBy = actor.Id,
                RecordedByFullName = RequestValidation.Text(actor.FullName, "RecordedByFullName", 200)
            };
            await payments.AddAsync(payment, token);
            invoice.PaidAmount = paid;
            invoice.RemainingAmount = invoice.TotalAmount - paid;
            invoice.PaymentStatus = status;
            await publisher.Publish(new InvoicePaymentRecorded(invoice, payment), token);
            return id;
        }, ct);
    }
}
