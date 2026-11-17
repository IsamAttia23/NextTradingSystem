using MapsterMapper;
using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.DTOs.Products;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Products;

public sealed class SaveProductHandler(IKeyRepository<Product, Guid> repository, IMapper mapper, IUnitOfWork unitOfWork,
    IQueryExecutor executor,
    IEntityValidator<ProductInputDto> validator, IEnumerable<IEntityRules<Product, ProductInputDto>> rules, ProductPricing pricing, IInventoryService inventory)
    : IRequestHandler<CreateEntityCommand<ProductDto, ProductInputDto>, ProductDto>,
      IRequestHandler<UpdateEntityCommand<ProductDto, ProductInputDto>, ProductDto>
{
    public Task<ProductDto> Handle(CreateEntityCommand<ProductDto, ProductInputDto> request, CancellationToken ct) =>
        unitOfWork.ExecuteInTransactionAsync(token => SaveAsync(request.Data, null, null, token), ct);

    public Task<ProductDto> Handle(UpdateEntityCommand<ProductDto, ProductInputDto> request, CancellationToken ct) =>
        unitOfWork.ExecuteInTransactionAsync(token => SaveAsync(request.Data, request.Id, request.RowVersion, token), ct);

    private async Task<ProductDto> SaveAsync(ProductInputDto input, Guid? id, byte[]? rowVersion, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (input is null)
        {
            RequestValidation.Fail("Data", "Input is required.");
        }
        if (id is { } recordId)
        {
            RequestValidation.Identifier(recordId);
        }
        foreach (var rule in rules)
        {
            rule.Normalize(input);
        }
        validator.Validate(input);
        var rate = await pricing.GetRateAsync(ct, input.ExchangeRate);

        Product? existing = null;
        if (id is { } productId)
        {
            existing = await repository.GetByIdAsync(productId, ct) ?? throw new NotFoundException("Record was not found.");
            repository.SetRowVersion(existing, rowVersion);
        }
        foreach (var rule in rules)
        {
            await rule.ValidateAsync(id, input, ct);
        }

        var storedCost = ProductPricing.ToStoredCost(input.CostPrice, rate, existing?.CostPrice);
        var product = existing is null ? mapper.Map<Product>(input) : mapper.Map(input, existing);
        product.Id = id ?? Guid.NewGuid();
        product.CostPrice = storedCost;
        if (existing is null)
        {
            await repository.AddAsync(product, ct);
        }
        ReceiveStockDto? opening = null;
        if (existing is null && input.AddOpeningStock)
        {
            opening = OpeningReceipt(product.Id, input);
        }
        await unitOfWork.SaveChangesAsync(ct);
        if (opening is not null)
        {
            await inventory.ReceiveAsync(opening, ct);
        }
        return await GetEntityByIdQueryHandler<Product, ProductDto>.ReadAsync(repository, executor,
            product.Id, pricing.ReadMapping(rate), ct);
    }

    private static ReceiveStockDto OpeningReceipt(Guid productId, ProductInputDto input)
    {
        if (input.OpeningWarehouseId is null || input.OpeningWarehouseId == Guid.Empty)
        {
            RequestValidation.Fail(nameof(input.OpeningWarehouseId), "OpeningWarehouseRequired");
        }
        if (input.OpeningQuantity is not > 0)
        {
            RequestValidation.Fail(nameof(input.OpeningQuantity), "OpeningQuantityRequired");
        }
        return new ReceiveStockDto
        {
            ProductId = productId,
            WarehouseId = input.OpeningWarehouseId.Value,
            Quantity = input.OpeningQuantity.Value,
            ReceiveInPackages = input.OpeningInPackages,
            BatchNumber = input.OpeningBatchNumber,
            ExpiryDate = input.OpeningExpiryDate
        };
    }
}
