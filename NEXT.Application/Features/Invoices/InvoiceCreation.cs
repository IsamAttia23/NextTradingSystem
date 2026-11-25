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

public sealed class InvoiceCreation(IInvoiceRepository invoices, InvoiceLookups lookups, InvoiceDraftBuilder builder, IInvoiceNumberGenerator numbers,
    ICurrentUserService currentUser, IUserQueryService users, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public Task<InvoiceSummary> CreateAsync(CreateInvoiceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        return unitOfWork.ExecuteReadCommittedAsync(async token =>
        {
            var account = await users.FindUserAsync(currentUser.UserId?.ToString() ?? "", token);
            if (account is null || !account.IsActive)
                RequestValidation.Fail("UserId", "تعذّر التحقق من المستخدم المنشئ للفاتورة.");
            var now = clock.GetLocalNow();
            var number = await numbers.NextAsync(DateOnly.FromDateTime(now.DateTime), token);
            var existing = await invoices.FirstOrDefaultAsync(item => item.Id == input.OperationId, token);
            if (existing is not null)
            {
                if (existing.UserId != account.Id)
                    throw new ConflictException("أعد فتح نموذج الفاتورة ثم حاول مجددًا.");
                return Summary(existing);
            }

            var fullName = RequestValidation.Text(account.FullName, "CreatedByFullName", 200);
            var warehouse = await lookups.DefaultWarehouseAsync(token);
            var draft = await builder.BuildAsync(input, warehouse, [], token);
            var invoice = new Invoice
            {
                Id = input.OperationId,
                InvoiceNumber = number,
                InvoiceDate = now.ToUniversalTime(),
                Type = InvoiceType.Sales,
                Status = InvoiceStatus.Pending,
                UserId = account.Id,
                CreatedByFullName = fullName,
                CustomerId = draft.CustomerId,
                CustomerName = draft.CustomerName,
                Notes = draft.Notes,
                WarehouseId = warehouse,
                Items = draft.Items,
                SubTotal = draft.Subtotal,
                DiscountType = input.DiscountType,
                DiscountValue = input.DiscountValue,
                DiscountAmount = draft.Discount,
                TotalAmount = draft.Total,
                RemainingAmount = draft.Total
            };
            await invoices.AddAsync(invoice, token);
            return Summary(invoice);
        }, ct);
    }

    private static InvoiceSummary Summary(Invoice invoice) => new(invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate,
        invoice.CustomerName, invoice.TotalAmount, invoice.Status, invoice.PaymentStatus);

}
