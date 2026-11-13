using NEXT.Application.DTOs.Products;
using NEXT.Application.Abstractions.Persistence;

using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Common.Validation;

namespace NEXT.Application.Features.Products;

public sealed class ProductRules(IReadRepository<Category> categories, IReadRepository<UnitOfMeasure> units, IReadRepository<Product> products) : IEntityRules<Product, ProductInputDto>
{
    public void Normalize(ProductInputDto input) => input.Barcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim();

    public async Task ValidateAsync(Guid? id, ProductInputDto input, CancellationToken ct)
    {
        foreach (var (field, value, precision, message) in new[]
        {
            (nameof(input.RetailPrice), input.RetailPrice, 12, "PercentagePrecision"),
            (nameof(input.WholesalePrice), input.WholesalePrice, 12, "PercentagePrecision"),
            (nameof(input.VipPrice), input.VipPrice, 12, "PercentagePrecision"),
            (nameof(input.TaxRate), input.TaxRate, 2, "TaxRatePrecision"),
            (nameof(input.MinStock), input.MinStock, 3, "StockQuantityPrecision"),
            (nameof(input.MaxStock), input.MaxStock.GetValueOrDefault(), 3, "StockQuantityPrecision")
        })
        {
            if (decimal.Round(value, precision) != value)
            {
                ReferenceGuard.Fail(field, message);
            }
        }
        var normalized = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim();
        if (input.CategoryId is { } category)
        {
            await ReferenceGuard.Require(categories, x => x.Id == category && x.IsActive, "CategoryId", ct);
        }
        if (input.UnitOfMeasureId is { } unit)
        {
            await ReferenceGuard.Require(units, x => x.Id == unit, "UnitOfMeasureId", ct);
        }
        if (input.MaxStock < input.MinStock)
        {
            ReferenceGuard.Fail("MaxStock", "Maximum stock must be at least the minimum.");
        }
        if (normalized is not null
            && await products.AnyAsync(x => x.Id != id
            && x.Barcode == normalized, ct))
        {
            ReferenceGuard.Fail("Barcode", "Barcode already exists.");
        }
        if (id is { } productId
            && await products.AnyAsync(x => x.Id == productId
            && x.HasExpiry != input.HasExpiry
            && x.StockMovements.Any(), ct))
        {
            ReferenceGuard.Fail("HasExpiry", "Expiry tracking cannot change after inventory movements exist.");
        }
    }
}
