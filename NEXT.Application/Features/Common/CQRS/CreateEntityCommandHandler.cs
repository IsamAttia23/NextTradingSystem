using NEXT.Application.DTOs.Products;
using NEXT.Application.Services.Currencies;
using NEXT.Domain.Entities;
using MapsterMapper;
using MediatR;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Media;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Application.Services;
using NEXT.Application.Validation;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class CreateEntityCommandHandler<TEntity, TDto, TCreationDto>(
    IKeyRepository<TEntity, Guid> repository, IMapper mapper, IUnitOfWork unitOfWork,
    IEntityValidator<TCreationDto> validator, IEnumerable<IEntityRules<TEntity, TCreationDto>> rules,
    IProfileImageProcessor? images = null, ExchangeRateReader? exchangeRates = null)
    : IRequestHandler<CreateEntityCommand<TDto, TCreationDto>, TDto>
    where TEntity : AuditableEntity<Guid> where TDto : class where TCreationDto : class
{
    public Task<TDto> Handle(CreateEntityCommand<TDto, TCreationDto> request, CancellationToken ct) =>
        typeof(TEntity) == typeof(Product)
            ? unitOfWork.ExecuteInTransactionAsync(token => SaveAsync(request, token), ct)
            : SaveAsync(request, ct);

    private async Task<TDto> SaveAsync(CreateEntityCommand<TDto, TCreationDto> request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Data is null)
        {
            RequestValidation.Fail("Data", "Input is required.");
        }
        foreach (var rule in rules)
        {
            rule.Normalize(request.Data);
        }
        validator.Validate(request.Data);
        var rate = await GetProductRateAsync(request.Data, ct);
        foreach (var rule in rules)
        {
            await rule.ValidateAsync(null, request.Data, ct);
        }
        var entity = mapper.Map<TEntity>(request.Data);
        entity.Id = Guid.NewGuid();
        SetCostPriceInUsd(entity, request.Data, rate, null);
        await EntityInput.SetImageAsync(entity, request.Data, images, ct);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        var result = mapper.Map<TDto>(entity);
        if (result is ProductDto productResult && rate is { } currentRate)
        {
            productResult.CostPrice *= currentRate;
            productResult.ExchangeRate = currentRate;
            productResult.RetailPrice = productResult.CostPrice * (1 + productResult.RetailPrice / 100m);
            productResult.WholesalePrice = productResult.CostPrice * (1 + productResult.WholesalePrice / 100m);
            productResult.VipPrice = productResult.CostPrice * (1 + productResult.VipPrice / 100m);
        }
        return result;
    }

    private async Task<decimal?> GetProductRateAsync(TCreationDto input, CancellationToken ct)
    {
        if (input is not ProductInputDto productInput || typeof(TEntity) != typeof(Product))
        {
            return null;
        }
        var reader = exchangeRates ?? throw new InvalidOperationException("Exchange rate reader is not registered.");
        var rate = await reader.GetUsdRateAsync(ct);
        if (productInput.ExchangeRate is { } expected && expected != rate)
        {
            throw new ConflictException("تغير سعر الدولار. أعد تحميل نموذج المنتج ثم حاول مرة أخرى.");
        }
        return rate;
    }

    private static void SetCostPriceInUsd(TEntity entity, TCreationDto input, decimal? rate, decimal? existingCost)
    {
        if (entity is not Product product || input is not ProductInputDto productInput || rate is not { } currentRate)
        {
            return;
        }
        // Preserve the USD base when the submitted SDG amount has not changed.
        if (existingCost is { } cost && productInput.CostPrice == cost * currentRate)
        {
            product.CostPrice = cost;
            return;
        }
        product.CostPrice = decimal.Round(productInput.CostPrice / currentRate, 8, MidpointRounding.AwayFromZero);
    }
}
