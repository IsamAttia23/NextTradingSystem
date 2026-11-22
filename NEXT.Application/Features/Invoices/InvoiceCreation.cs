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

public sealed class InvoiceCreation(IInvoiceRepository invoices, InvoiceLookups lookups, IInvoiceNumberGenerator numbers,
    ICurrentUserService currentUser, IUserQueryService users, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public Task<InvoiceSummary> CreateAsync(CreateInvoiceInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        var name = RequestValidation.Text(input.CustomerName, nameof(input.CustomerName), 200);
        var notes = string.IsNullOrWhiteSpace(input.Notes) ? null : RequestValidation.Text(input.Notes, nameof(input.Notes), 2000);
        var lines = input.Lines?.Where(line => line.ProductId.HasValue || line.Quantity.HasValue || line.QuotedUnitPrice.HasValue).ToArray();
        if (lines is not { Length: > 0 })
            RequestValidation.Fail("Lines", "أضف بندًا واحدًا على الأقل.");

        foreach (var line in lines)
        {
            if (line.ProductId is null || line.ProductId == Guid.Empty || line.QuotedUnitPrice is null ||
                line.Quantity is not { } quantity || quantity is <= 0 or > InvoiceAmounts.MaximumQuantity ||
                decimal.Round(quantity, 3) != quantity)
            {
                RequestValidation.Fail("Lines", "اختر منتجًا صالحًا وأدخل كمية موجبة بثلاث منازل عشرية بحد أقصى.");
            }
        }

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
            var customer = await lookups.ResolveCustomerAsync(input.CustomerId, name, token);
            var items = await BuildItemsAsync(lines, warehouse, token);
            var subtotal = InvoiceAmounts.Money(items.Sum(item => item.TotalAmount));
            var discount = InvoiceAmounts.Discount(subtotal, input.DiscountType, input.DiscountValue);
            var invoice = new Invoice
            {
                Id = input.OperationId,
                InvoiceNumber = number,
                InvoiceDate = now.ToUniversalTime(),
                Type = InvoiceType.Sales,
                Status = InvoiceStatus.Pending,
                UserId = account.Id,
                CreatedByFullName = fullName,
                CustomerId = customer?.Id,
                CustomerName = customer?.Name ?? name,
                Notes = notes,
                WarehouseId = warehouse,
                Items = items,
                SubTotal = subtotal,
                DiscountType = input.DiscountType,
                DiscountValue = input.DiscountValue,
                DiscountAmount = discount,
                TotalAmount = subtotal - discount,
                RemainingAmount = subtotal - discount
            };
            await invoices.AddAsync(invoice, token);
            return Summary(invoice);
        }, ct);
    }

    private static InvoiceSummary Summary(Invoice invoice) => new(invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate,
        invoice.CustomerName, invoice.TotalAmount, invoice.Status, invoice.PaymentStatus);

    private async Task<List<InvoiceItem>> BuildItemsAsync(InvoiceLineInput[] lines, Guid warehouse, CancellationToken ct)
    {
        var quantities = lines.GroupBy(line => line.ProductId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity!.Value));
        var products = (await lookups.FindProductsAsync(quantities.Keys.ToArray(), warehouse, ct))
            .ToDictionary(product => product.Id);
        foreach (var (id, quantity) in quantities)
        {
            if (quantity > InvoiceAmounts.MaximumQuantity)
                RequestValidation.Fail("Lines", "إجمالي كمية المنتج يتجاوز الحد المسموح.");
            if (!products.TryGetValue(id, out var product))
                RequestValidation.Fail("Lines", "أحد المنتجات المختارة لم يعد متاحًا.");
            if (quantity > product.AvailableQuantity)
                RequestValidation.Fail("Lines", $"الكمية المطلوبة تتجاوز المخزون المتاح للمنتج: {product.Name}.");
        }

        foreach (var line in lines)
        {
            var product = products[line.ProductId!.Value];
            if (line.QuotedUnitPrice != product.UnitPrice)
                throw new ConflictException($"تغيّر سعر المنتج {product.Name}. اختر المنتج مجددًا لتحديث السعر.");
        }

        var items = new List<InvoiceItem>(quantities.Count);
        foreach (var (id, quantity) in quantities)
        {
            var product = products[id];
            items.Add(new InvoiceItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = product.UnitPrice,
                UnitCost = product.UnitCost,
                TotalAmount = InvoiceAmounts.Money(quantity * product.UnitPrice)
            });
        }
        return items;
    }
}
