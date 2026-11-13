using Mapster;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.DTOs.Products;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Services.Currencies;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Products;

public sealed class ProductPricing(ExchangeRateReader rates, TypeAdapterConfig mapping) : IActivityPageEnricher
{
    public async Task<decimal> GetRateAsync(CancellationToken ct, decimal? expectedRate = null)
    {
        var rate = await rates.GetUsdRateAsync(ct);
        if (expectedRate is { } expected && expected != rate)
        {
            throw new ConflictException("تغير سعر الدولار. أعد تحميل نموذج المنتج ثم حاول مرة أخرى.");
        }
        return rate;
    }

    public static decimal ToStoredCost(decimal cost, decimal rate, decimal? previousCost) =>
        previousCost is { } stored && cost == stored * rate
            ? stored
            : decimal.Round(cost / rate, 8, MidpointRounding.AwayFromZero);

    public TypeAdapterConfig ReadMapping(decimal rate)
    {
        var config = mapping.Clone();
        config.ForType<Product, ProductDto>()
            .Map(destination => destination.CostPrice, source => source.CostPrice * rate)
            .Map(destination => destination.ExchangeRate, source => (decimal?)rate)
            .Map(destination => destination.RetailPrice,
                source => source.CostPrice * rate * (1 + source.RetailPrice / 100m))
            .Map(destination => destination.WholesalePrice,
                source => source.CostPrice * rate * (1 + source.WholesalePrice / 100m))
            .Map(destination => destination.VipPrice,
                source => source.CostPrice * rate * (1 + source.VipPrice / 100m));
        return config;
    }

    public TypeAdapterConfig EditMapping(decimal rate)
    {
        var config = mapping.Clone();
        config.ForType<Product, ProductEditDto>()
            .Map(destination => destination.CostPrice, source => source.CostPrice * rate)
            .Map(destination => destination.ExchangeRate, source => (decimal?)rate);
        return config;
    }

    public async Task<ActivityPageDto> EnrichAsync(ActivityPageDto page, CancellationToken ct)
    {
        if (page.Selected != nameof(Product) || page.Records.Count == 0)
        {
            return page;
        }
        var rate = await GetRateAsync(ct);
        var records = page.Records.Select(record =>
        {
            var values = record.Values.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (values.TryGetValue(nameof(Product.CostPrice), out var value) && value is decimal cost)
            {
                values[nameof(Product.CostPrice)] = cost * rate;
            }
            return record with { Values = values };
        }).ToArray();
        return page with { Records = records };
    }
}
