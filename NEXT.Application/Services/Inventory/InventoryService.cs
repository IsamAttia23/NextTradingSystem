using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Application.Services.Inventory;

/// <summary>All balance, batch and ledger writes occur in one serializable transaction.</summary>
public sealed class InventoryService(IKeyRepository<Product, Guid> products, IKeyRepository<Warehouse, Guid> warehouses,
    IRepository<Stock> stocks, IRepository<StockMovement> movements, IRepository<ProductBatch> batches,
    IUnitOfWork unitOfWork, IEntityValidator<ReceiveStockDto> receiptValidator,
    IEntityValidator<TransferStockDto> transferValidator) : IInventoryService
{
    public Task ReceiveAsync(ReceiveStockDto input, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        receiptValidator.Validate(input);
        ValidateOperation(input);
        if (decimal.Round(input.UnitCost, 2) != input.UnitCost)
        {
            Fail("UnitCost", "Cost supports at most two decimal places.");
        }
        return unitOfWork.ExecuteInTransactionAsync(token => ReceiveInTransactionAsync(input, token), ct);
    }

    private async Task ReceiveInTransactionAsync(ReceiveStockDto input, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await EnsureNewOperation(input.OperationId, ct);
        var product = await Product(input.ProductId, ct);
        await Warehouse(input.WarehouseId, ct);
        ValidateReceiptExpiry(product, input, DateOnly.FromDateTime(now.UtcDateTime));
        var stock = await GetOrCreateStock(input.ProductId, input.WarehouseId, ct);
        EnsureCapacity(stock.Quantity, input.Quantity);
        if (product.HasExpiry)
        {
            await ReceiveBatchAsync(input, ct);
        }
        Receive(stock, input.Quantity, input.UnitCost, now);
        await movements.AddAsync(Movement(input, input.WarehouseId, StockMovementType.Purchase, input.UnitCost, "Receipt", now), ct);
    }

    private static void ValidateReceiptExpiry(Product product, ReceiveStockDto input, DateOnly today)
    {
        ValidateBatch(product, input.BatchNumber);
        if (product.HasExpiry
            && (input.ExpiryDate is null
                || input.ExpiryDate < today))
        {
            Fail("ExpiryDate", "Select a non-expired expiry date.");
        }
        if (!product.HasExpiry && input.ExpiryDate is not null)
        {
            Fail("ExpiryDate", "This product does not track expiry dates.");
        }
    }

    private async Task ReceiveBatchAsync(ReceiveStockDto input, CancellationToken ct)
    {
        var batch = await GetOrCreateBatch(input.ProductId, input.WarehouseId, input.BatchNumber!.Trim(), input.ExpiryDate, ct);
        EnsureCapacity(batch.Quantity, input.Quantity);
        batch.CostPrice = InventoryValuation.ReceiveCost(batch.Quantity, batch.CostPrice, input.Quantity, input.UnitCost);
        batch.Quantity += input.Quantity;
    }

    public Task TransferAsync(TransferStockDto input, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        transferValidator.Validate(input);
        ValidateOperation(input);
        if (input.SourceWarehouseId == input.DestinationWarehouseId)
        {
            Fail("DestinationWarehouseId", "Choose a different destination warehouse.");
        }
        return unitOfWork.ExecuteInTransactionAsync(token => TransferInTransactionAsync(input, token), ct);
    }

    private async Task TransferInTransactionAsync(TransferStockDto input, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await EnsureNewOperation(input.OperationId, ct);
        var product = await Product(input.ProductId, ct);
        await Warehouse(input.SourceWarehouseId, ct);
        await Warehouse(input.DestinationWarehouseId, ct);
        ValidateBatch(product, input.BatchNumber);
        var source = await stocks.FindTrackedAsync(x => x.ProductId == input.ProductId && x.WarehouseId == input.SourceWarehouseId, ct);
        if (source is null || source.AvailableQuantity < input.Quantity)
        {
            Fail("Quantity", "Insufficient available stock; reserved quantities cannot be moved.");
        }
        var target = await GetOrCreateStock(input.ProductId, input.DestinationWarehouseId, ct);
        EnsureCapacity(target.Quantity, input.Quantity);
        var cost = source.AverageCost;
        if (product.HasExpiry)
        {
            await TransferBatchAsync(input, cost, DateOnly.FromDateTime(now.UtcDateTime), ct);
        }
        source.Quantity -= input.Quantity;
        source.LastUpdated = now;
        Receive(target, input.Quantity, cost, now);
        await RecordTransferAsync(input, cost, now, ct);
    }

    private async Task TransferBatchAsync(TransferStockDto input, decimal cost, DateOnly today, CancellationToken ct)
    {
        var number = input.BatchNumber!.Trim();
        var batch = await batches.FindTrackedAsync(x => x.ProductId == input.ProductId && x.WarehouseId == input.SourceWarehouseId && x.BatchNumber == number, ct);
        if (batch is null || batch.Quantity < input.Quantity)
        {
            Fail("BatchNumber", "Insufficient quantity in the selected batch.");
        }
        if (batch.ExpiryDate is null || batch.ExpiryDate < today)
        {
            Fail("BatchNumber", "Expired batches cannot be transferred.");
        }
        var destination = await GetOrCreateBatch(input.ProductId, input.DestinationWarehouseId, number, batch.ExpiryDate, ct);
        EnsureCapacity(destination.Quantity, input.Quantity);
        destination.CostPrice = InventoryValuation.ReceiveCost(destination.Quantity, destination.CostPrice, input.Quantity, cost);
        destination.Quantity += input.Quantity;
        batch.Quantity -= input.Quantity;
    }

    private async Task RecordTransferAsync(TransferStockDto input, decimal cost, DateTimeOffset now, CancellationToken ct)
    {
        await movements.AddAsync(Movement(input, input.SourceWarehouseId, StockMovementType.TransferOut, cost, "Transfer", now), ct);
        await movements.AddAsync(Movement(input, input.DestinationWarehouseId, StockMovementType.TransferIn, cost, "Transfer", now), ct);
    }

    private async Task<Product> Product(Guid id, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(id, ct) ?? throw new NotFoundException("Product was not found.");
        if (!product.IsActive)
        {
            Fail("ProductId", "Select an active product.");
        }
        return product;
    }
    private async Task Warehouse(Guid id, CancellationToken ct)
    {
        var warehouse = await warehouses.GetByIdAsync(id, ct) ?? throw new NotFoundException("Warehouse was not found.");
        if (!warehouse.IsActive)
        {
            Fail("WarehouseId", "Select an active warehouse.");
        }
    }
    private async Task EnsureNewOperation(Guid id, CancellationToken ct)
    {
        if (await movements.AnyAsync(x => x.ReferenceId == id
            && (x.ReferenceType == "Receipt"
                || x.ReferenceType == "Transfer"), ct))
        {
            throw new ConflictException("This inventory operation has already been recorded.");
        }
    }
    private async Task<Stock> GetOrCreateStock(Guid product, Guid warehouse, CancellationToken ct)
    {
        var stock = await stocks.FindTrackedAsync(x => x.ProductId == product && x.WarehouseId == warehouse, ct);
        if (stock is not null)
        {
            return stock;
        }
        stock = new Stock
        {
            Id = Guid.NewGuid(),
            ProductId = product,
            WarehouseId = warehouse
        };
        await stocks.AddAsync(stock, ct);
        return stock;
    }
    private async Task<ProductBatch> GetOrCreateBatch(Guid product, Guid warehouse, string number, DateOnly? expiry, CancellationToken ct)
    {
        var batch = await batches.FindTrackedAsync(x => x.ProductId == product && x.WarehouseId == warehouse && x.BatchNumber == number, ct);
        if (batch is not null)
        {
            if (batch.ExpiryDate != expiry)
            {
                Fail("BatchNumber", "The existing batch has a different expiry date.");
            }
            return batch;
        }
        batch = new ProductBatch
        {
            Id = Guid.NewGuid(),
            ProductId = product,
            WarehouseId = warehouse,
            BatchNumber = number,
            ExpiryDate = expiry
        };
        await batches.AddAsync(batch, ct);
        return batch;
    }
    private static void Receive(Stock stock, decimal quantity, decimal cost, DateTimeOffset now)
    {
        stock.AverageCost = InventoryValuation.ReceiveCost(stock.Quantity, stock.AverageCost, quantity, cost);
        stock.Quantity += quantity;
        stock.LastUpdated = now;
    }
    private static StockMovement Movement(InventoryOperationDto input, Guid warehouse, StockMovementType type, decimal cost, string reference, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = now,
        ProductId = input.ProductId,
        WarehouseId = warehouse,
        Type = type,
        Quantity = input.Quantity,
        UnitCost = cost,
        ReferenceId = input.OperationId,
        ReferenceType = reference,
        Notes = string.IsNullOrWhiteSpace(input.BatchNumber) ? input.Notes : "Batch: " + input.BatchNumber.Trim() + "\n" + input.Notes
    };
    private static void ValidateBatch(Product product, string? number)
    {
        if (product.HasExpiry && string.IsNullOrWhiteSpace(number))
        {
            Fail("BatchNumber", "An expiry-tracked product requires a batch number.");
        }
        if (!product.HasExpiry && !string.IsNullOrWhiteSpace(number))
        {
            Fail("BatchNumber", "Batch tracking requires an expiry-tracked product.");
        }
    }
    private static void ValidateOperation(InventoryOperationDto input)
    {
        if (input.OperationId == Guid.Empty)
        {
            Fail("OperationId", "An operation ID is required.");
        }
        if (decimal.Round(input.Quantity, 3) != input.Quantity)
        {
            Fail("Quantity", "Quantity supports at most three decimal places.");
        }
    }
    private static void EnsureCapacity(decimal quantity, decimal incoming)
    {
        if (quantity + incoming > InventoryValuation.MaximumQuantity)
        {
            Fail("Quantity", "The resulting quantity exceeds storage capacity.");
        }
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string field, string message) => throw new AppValidationException(new Dictionary<string, string[]>
    {
        [field] = [message]
    });
}
