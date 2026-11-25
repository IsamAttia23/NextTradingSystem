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

namespace NEXT.Application.Features.Invoices;

public sealed class InvoiceEditing(IInvoiceRepository invoices, IInvoiceConfirmationStore store, InvoiceLookups lookups,
    InvoiceDraftBuilder builder, IRepository<InvoiceStatusHistory> history, IReadRepository<Payment> payments,
    IQueryExecutor executor, ICurrentUserService currentUser, IUserQueryService users, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<InvoiceEditDetails> GetAsync(Guid id, CancellationToken ct)
    {
        RequestValidation.Identifier(id);
        var invoice = await invoices.GetDetailsAsync(id, ct) ?? throw new NotFoundException("InvoiceNotFound");
        EnsureEditable(invoice);
        var products = (await lookups.FindProductsAsync(invoice.Items.Select(item => item.ProductId).Distinct().ToArray(), invoice.WarehouseId, ct))
            .ToDictionary(item => item.Id);
        var input = new EditInvoiceInput { OperationId = Guid.NewGuid(), RowVersion = invoice.RowVersion,
            CustomerId = invoice.CustomerId, CustomerName = invoice.CustomerName ?? "", Notes = invoice.Notes,
            DiscountType = invoice.DiscountType, DiscountValue = invoice.DiscountValue,
            Lines = invoice.Items.Select(item => new InvoiceLineInput { ProductId = item.ProductId,
                Quantity = item.Quantity, QuotedUnitPrice = item.UnitPrice }).ToList() };
        return new(input, invoice.Items.Select(item => new InvoiceProductOption(item.ProductId, item.Product?.Name ?? "—",
            item.Product?.Barcode, item.UnitPrice, item.UnitCost, products.GetValueOrDefault(item.ProductId)?.AvailableQuantity ?? 0)).ToArray());
    }

    public Task<Guid> EditAsync(Guid id, EditInvoiceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(id);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        if (input.RowVersion is not { Length: > 0 }) RequestValidation.Fail(nameof(input.RowVersion), "InvoiceEditConflict");
        var hash = InvoiceOperationFingerprint.Of(new { input.CustomerId, input.CustomerName, input.Notes,
            input.DiscountType, input.DiscountValue, input.Lines });
        return unitOfWork.ExecuteReadCommittedAsync(async token =>
        {
            var actor = await users.FindUserAsync(currentUser.UserId?.ToString() ?? "", token);
            if (actor is null || !actor.IsActive) RequestValidation.Fail("UserId", "InvoiceActorUnavailable");
            var invoice = await store.LockInvoiceAsync(id, token) ?? throw new NotFoundException("InvoiceNotFound");
            var previous = await history.FirstOrDefaultAsync(item => item.InvoiceId == id && item.OperationId == input.OperationId, token);
            if (previous is not null)
            {
                if (previous.ActorId != actor.Id || previous.FromStatus != InvoiceStatus.Pending || previous.ToStatus != InvoiceStatus.Pending || previous.RequestHash != hash)
                    throw new ConflictException("InvoiceEditConflict");
                return id;
            }
            if (await payments.AnyAsync(item => item.Id == input.OperationId, token)) throw new ConflictException("InvoiceEditConflict");
            EnsureEditable(invoice);
            invoices.SetRowVersion(invoice, input.RowVersion);
            var recorded = await executor.ToListAsync(payments.Query(item => item.InvoiceId == id), token);
            if (recorded.Any(item => item.PaymentType != PaymentType.Receipt || item.Amount <= 0) ||
                recorded.Sum(item => item.Amount) != invoice.PaidAmount || invoice.RemainingAmount != invoice.TotalAmount - invoice.PaidAmount)
                throw new ConflictException("InvoicePaymentConflict");
            var draft = await builder.BuildAsync(input, invoice.WarehouseId, invoice.Items.ToArray(), token);
            if (invoice.PaidAmount > 0 && (input.CustomerId != invoice.CustomerId || input.CustomerName.Trim() != invoice.CustomerName))
                RequestValidation.Fail("CustomerName", "InvoicePaidCustomerImmutable");
            if (invoice.PaidAmount > 0) draft = draft with { CustomerId = invoice.CustomerId, CustomerName = invoice.CustomerName! };
            if (draft.Total < invoice.PaidAmount) RequestValidation.Fail("DiscountValue", "InvoiceTotalBelowPaid");
            invoice.CustomerId = draft.CustomerId; invoice.CustomerName = draft.CustomerName; invoice.Notes = draft.Notes;
            invoice.Items = draft.Items; invoice.SubTotal = draft.Subtotal; invoice.DiscountType = input.DiscountType;
            invoice.DiscountValue = input.DiscountValue; invoice.DiscountAmount = draft.Discount; invoice.TotalAmount = draft.Total;
            invoice.RemainingAmount = draft.Total - invoice.PaidAmount;
            invoice.PaymentStatus = InvoicePaymentRules.Status(draft.Total, invoice.PaidAmount);
            var now = clock.GetUtcNow(); invoice.LastActionAt = now;
            await history.AddAsync(new InvoiceStatusHistory { InvoiceId = id, OperationId = input.OperationId,
                FromStatus = invoice.Status, ToStatus = invoice.Status, ActionAt = now, ActorId = actor.Id,
                ActorFullName = RequestValidation.Text(actor.FullName, "ActorFullName", 200), Reason = "InvoiceEdited", RequestHash = hash }, token);
            return id;
        }, ct);
    }

    private static void EnsureEditable(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.Pending || invoice.Type != InvoiceType.Sales || invoice.LegacyInvoiceTypeCode is not null ||
            invoice.ParentInvoiceId is not null || invoice.TaxAmount != 0 || invoice.Items.Any(item => item.TaxAmount != 0))
            throw new ConflictException("InvoiceEditUnavailable");
    }
}
