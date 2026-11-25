using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Invoices;

// Creation and pending-invoice editing share validation, product merging, pricing and rounding.
public sealed class InvoiceDraftBuilder(InvoiceLookups lookups)
{
    public async Task<InvoiceDraft> BuildAsync(InvoiceDraftInput input, Guid warehouse,
        IReadOnlyCollection<InvoiceItem> existing, CancellationToken ct)
    {
        var name = RequestValidation.Text(input.CustomerName, nameof(input.CustomerName), 200);
        var notes = string.IsNullOrWhiteSpace(input.Notes) ? null : RequestValidation.Text(input.Notes, nameof(input.Notes), 2000);
        var lines = input.Lines?.Where(line => line.ProductId.HasValue || line.Quantity.HasValue || line.QuotedUnitPrice.HasValue).ToArray();
        if (lines is not { Length: > 0 }) RequestValidation.Fail("Lines", "InvoiceLinesRequired");
        foreach (var line in lines)
            if (line.ProductId is null || line.ProductId == Guid.Empty || line.QuotedUnitPrice is null ||
                line.Quantity is not { } quantity || quantity is <= 0 or > InvoiceAmounts.MaximumQuantity || decimal.Round(quantity, 3) != quantity)
                RequestValidation.Fail("Lines", "InvoiceLinesInvalid");
        var groups = lines.GroupBy(line => line.ProductId!.Value).ToArray();
        var products = (await lookups.FindProductsAsync(groups.Select(group => group.Key).ToArray(), warehouse, ct))
            .ToDictionary(product => product.Id);
        var items = new List<InvoiceItem>(groups.Length);
        foreach (var group in groups)
        {
            var quantity = group.Sum(line => line.Quantity!.Value);
            if (quantity > InvoiceAmounts.MaximumQuantity) RequestValidation.Fail("Lines", "InvoiceLinesInvalid");
            if (!products.TryGetValue(group.Key, out var product)) RequestValidation.Fail("Lines", "InvoiceProductUnavailable");
            if (quantity > product.AvailableQuantity) RequestValidation.Fail("Lines", "InvoiceInsufficientStock");
            var quote = group.First().QuotedUnitPrice!.Value;
            var old = existing.Where(item => item.ProductId == group.Key).ToArray();
            // Editing notes/quantities preserves existing prices; selecting a product again can explicitly refresh its quote.
            var preserved = old.Length > 0 && old.All(item => item.UnitPrice == quote && item.UnitCost == old[0].UnitCost);
            if (group.Any(line => line.QuotedUnitPrice != quote) || (!preserved && quote != product.UnitPrice))
                throw new ConflictException("InvoicePriceChanged");
            items.Add(new InvoiceItem { ProductId = group.Key, Quantity = quantity, UnitPrice = quote,
                UnitCost = preserved ? old[0].UnitCost : product.UnitCost, TotalAmount = InvoiceAmounts.Money(quantity * quote) });
        }
        var customer = await lookups.ResolveCustomerAsync(input.CustomerId, name, ct);
        var subtotal = InvoiceAmounts.Money(items.Sum(item => item.TotalAmount));
        var discount = InvoiceAmounts.Discount(subtotal, input.DiscountType, input.DiscountValue);
        return new(customer?.Id, customer?.Name ?? name, notes, items, subtotal, discount, subtotal - discount);
    }
}

