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

public sealed class UpdateEntityCommandHandler<TEntity, TDto, TUpdateDto>(
    IKeyRepository<TEntity, Guid> repository, IMapper mapper, IUnitOfWork unitOfWork,
    IEntityValidator<TUpdateDto> validator, IEnumerable<IEntityRules<TEntity, TUpdateDto>> rules,
    IProfileImageProcessor? images = null, ExchangeRateReader? exchangeRates = null)
    : IRequestHandler<UpdateEntityCommand<TDto, TUpdateDto>, TDto>
    where TEntity : AuditableEntity<Guid> where TDto : class where TUpdateDto : class
{
    public Task<TDto> Handle(UpdateEntityCommand<TDto, TUpdateDto> request, CancellationToken ct) =>
        typeof(TEntity) == typeof(Product)
            ? unitOfWork.ExecuteInTransactionAsync(token => SaveAsync(request, token), ct)
            : SaveAsync(request, ct);

    private async Task<TDto> SaveAsync(UpdateEntityCommand<TDto, TUpdateDto> request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Data is null)
        {
            RequestValidation.Fail("Data", "Input is required.");
        }
        RequestValidation.Identifier(request.Id);
        if (request.Data is TIdType<Guid> keyed)
        {
            keyed.Id = request.Id;
        }
        foreach (var rule in rules)
        {
            rule.Normalize(request.Data);
        }
        validator.Validate(request.Data);
        var rate = await GetProductRateAsync(request.Data, ct);
        var entity = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        repository.SetRowVersion(entity, request.RowVersion);
        foreach (var rule in rules)
        {
            await rule.ValidateAsync(request.Id, request.Data, ct);
        }
        var existing = (entity as IHasProfileImage)?.AvatarUrl;
        var existingCost = entity is Product product ? product.CostPrice : (decimal?)null;
        mapper.Map(request.Data, entity);
        SetCostPriceInUsd(entity, request.Data, rate, existingCost);
        entity.Id = request.Id;
        await EntityInput.SetImageAsync(entity, request.Data, images, ct, existing);
        await unitOfWork.SaveChangesAsync(ct);
        var result = mapper.Map<TDto>(entity);
        if (result is ProductDto productResult && rate is { } currentRate)
        {
            productResult.CostPrice *= currentRate;
            productResult.ExchangeRate = currentRate;
        }
        return result;
    }

    private async Task<decimal?> GetProductRateAsync(TUpdateDto input, CancellationToken ct)
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

    private static void SetCostPriceInUsd(TEntity entity, TUpdateDto input, decimal? rate, decimal? existingCost)
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
