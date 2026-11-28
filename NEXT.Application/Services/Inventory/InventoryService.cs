using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Features.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Application.Services.Currencies;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Application.Services.Inventory;

/// <summary>Balance, batch and ledger writes share one transaction per inventory operation.</summary>
public sealed class InventoryService(IKeyRepository<Product, Guid> products, IKeyRepository<Warehouse, Guid> warehouses,
    IRepository<Stock> stocks, IRepository<StockMovement> movements, IRepository<ProductBatch> batches,
    IUnitOfWork unitOfWork, IEntityValidator<ReceiveStockDto> receiptValidator,
    IEntityValidator<TransferStockDto> transferValidator, ExchangeRateReader exchangeRates,
    IInvoiceConfirmationStore invoiceStore, IAccountingPostingService posting,
    IKeyRepository<Supplier, Guid> suppliers, TimeProvider clock, NEXT.Application.Features.Invoices.InvoiceStockPolicy stockPolicy) : IInventoryService
{
    public async Task IssueReservedInvoiceAsync(InvoiceDispatchInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(input.InvoiceId, nameof(input.InvoiceId));
        RequestValidation.Identifier(input.WarehouseId, nameof(input.WarehouseId));
        if (input.Quantities is null || input.Quantities.Count == 0 || input.Quantities.Any(item => item.Key == Guid.Empty ||
            item.Value <= 0 || item.Value > InventoryValuation.MaximumQuantity || decimal.Round(item.Value, 3) != item.Value))
            Fail("Lines", "InvoiceLinesInvalid");
        var quantities = input.Quantities.ToDictionary(item => item.Key, item => item.Value);
        var productIds = quantities.Keys.Order().ToArray();
        var lockedStocks = await invoiceStore.LockStocksAsync(input.WarehouseId, productIds, ct);
        if (await movements.AnyAsync(item => item.ReferenceType == "Invoice" && item.ReferenceId == input.InvoiceId &&
            item.Type == StockMovementType.Sale, ct))
            throw new ConflictException("InvoiceFulfillmentConflict");
        var warehouse = await warehouses.GetByIdAsync(input.WarehouseId, ct);
        if (warehouse is null || !warehouse.IsActive)
            Fail("WarehouseId", "InvoiceWarehouseUnavailable");
        var selectedProducts = new Dictionary<Guid, Product>();
        foreach (var id in productIds)
        {
            var product = await products.GetByIdAsync(id, ct);
            if (product is null || !product.IsActive)
                Fail("Lines", "InvoiceProductUnavailable");
            selectedProducts.Add(id, product);
        }
        if (lockedStocks.Count != quantities.Count || lockedStocks.Any(stock =>
            (!stockPolicy.AllowOverselling && stock.Quantity < quantities[stock.ProductId]) || stock.ReservedQuantity < quantities[stock.ProductId]))
            Fail("Lines", "InvoiceReservedStockInsufficient");
        var today = DateOnly.FromDateTime(input.ActionAt.UtcDateTime);
        var expiryProducts = productIds.Where(id => selectedProducts[id].HasExpiry).ToArray();
        var lockedBatches = expiryProducts.Length == 0 ? Array.Empty<ProductBatch>() :
            await invoiceStore.LockBatchesAsync(input.WarehouseId, expiryProducts, today, ct);
        var rate = await exchangeRates.GetUsdRateAsync(ct);
        foreach (var stock in lockedStocks)
        {
            var quantity = quantities[stock.ProductId];
            var unitCost = stock.AverageCost > 0 ? stock.AverageCost : MoneyRules.Round(selectedProducts[stock.ProductId].CostPrice * rate);
            var issuedCost = stockPolicy.AllowOverselling
                ? TrialInventoryValuation.Issue(stock.Quantity, stock.TotalCost, quantity, unitCost)
                : InventoryValuation.IssueValue(stock.Quantity, stock.TotalCost, quantity);
            var unallocatedQuantity = quantity;
            var unallocatedCost = issuedCost;
            if (selectedProducts[stock.ProductId].HasExpiry)
            {
                var candidates = lockedBatches.Where(batch => batch.ProductId == stock.ProductId)
                    .OrderBy(batch => batch.ExpiryDate).ThenBy(batch => batch.Id).ToArray();
                if (!stockPolicy.AllowOverselling && candidates.Sum(batch => batch.Quantity) < quantity)
                    Fail("Lines", "InvoiceValidBatchesInsufficient");
                var remaining = quantity;
                foreach (var batch in candidates)
                {
                    var issued = Math.Min(remaining, batch.Quantity);
                    batch.Quantity -= issued;
                    var batchCost = InventoryValuation.IssueValue(unallocatedQuantity, unallocatedCost, issued);
                    var movement = Movement(stock.ProductId, issued, input.InvoiceId, input.WarehouseId,
                        StockMovementType.Sale, stock.AverageCost, "Invoice", input.ActionAt, "Batch: " + batch.BatchNumber);
                    movement.TotalCost = batchCost;
                    await movements.AddAsync(movement, ct);
                    unallocatedQuantity -= issued;
                    unallocatedCost -= batchCost;
                    remaining -= issued;
                    if (remaining == 0) break;
                }
                if (remaining > 0)
                {
                    var shortage = Movement(stock.ProductId, remaining, input.InvoiceId, input.WarehouseId, StockMovementType.Sale, unitCost, "Invoice", input.ActionAt, "Trial shortage");
                    shortage.TotalCost = unallocatedCost;
                    await movements.AddAsync(shortage, ct);
                }
            }
            else
            {
                var movement = Movement(stock.ProductId, quantity, input.InvoiceId, input.WarehouseId,
                    StockMovementType.Sale, stock.AverageCost, "Invoice", input.ActionAt);
                movement.TotalCost = issuedCost;
                await movements.AddAsync(movement, ct);
            }
            stock.TotalCost -= issuedCost;
            stock.Quantity -= quantity;
            stock.AverageCost = stock.Quantity == 0 ? unitCost : InventoryValuation.Average(stock.Quantity, stock.TotalCost);
            stock.ReservedQuantity -= quantity;
            stock.LastUpdated = input.ActionAt;
        }
    }

    public Task ReceiveAsync(ReceiveStockDto input, CancellationToken ct = default) => ReceiveAsync(input, false, ct);

    // Only the product creation workflow invokes this; the receipt HTTP form cannot choose equity as its counteraccount.
    public Task ReceiveOpeningAsync(ReceiveStockDto input, CancellationToken ct = default) => ReceiveAsync(input, true, ct);

    private Task ReceiveAsync(ReceiveStockDto input, bool opening, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        receiptValidator.Validate(input);
        ValidateOperation(input);
        return unitOfWork.ExecuteInTransactionAsync(token => ReceiveInTransactionAsync(input, opening, token), ct);
    }

    private async Task ReceiveInTransactionAsync(ReceiveStockDto input, bool opening, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await EnsureNewOperation(input.OperationId, ct);
        var product = await Product(input.ProductId, ct);
        await Warehouse(input.WarehouseId, ct);
        if (!opening)
        {
            if (input.SupplierId is not { } supplierId || supplierId == Guid.Empty)
                Fail(nameof(input.SupplierId), "InventorySupplierRequired");
            var supplier = await suppliers.GetByIdAsync(input.SupplierId!.Value, ct);
            if (supplier is null || !supplier.IsActive || supplier.DeletedAt.HasValue)
                Fail(nameof(input.SupplierId), "InventorySupplierUnavailable");
        }
        ValidateReceiptExpiry(product, input, DateOnly.FromDateTime(now.UtcDateTime));
        var quantity = ReceiptQuantity(product, input);
        EnsureCapacity(0, quantity);
        var rate = await exchangeRates.GetUsdRateAsync(ct);
        var cost = decimal.Round(product.CostPrice * rate, 2, MidpointRounding.AwayFromZero);
        if (cost < 0 || cost > 9999999999999.99m)
        {
            Fail("ProductId", "ReceiptCostOutOfRange");
        }
        var stock = await GetOrCreateStock(input.ProductId, input.WarehouseId, ct);
        EnsureCapacity(stock.Quantity, quantity);
        if (product.HasExpiry)
        {
            await ReceiveBatchAsync(input, quantity, cost, ct);
        }
        decimal value;
        try { value = InventoryValuation.ReceiptValue(quantity, cost); }
        catch (ArgumentOutOfRangeException) { Fail("ProductId", "ReceiptCostOutOfRange"); throw; }
        var correction = TrialInventoryValuation.ReceiptAdjustment(stock.Quantity, stock.TotalCost, quantity, cost);
        Receive(stock, quantity, value - correction, now);
        var movement = Movement(input, input.WarehouseId, opening ? StockMovementType.OpeningBalance : StockMovementType.Purchase, cost, "Receipt", now);
        movement.Quantity = quantity;
        movement.TotalCost = value - correction;
        movement.CostAdjustment = correction;
        movement.SupplierId = opening ? null : input.SupplierId;
        await movements.AddAsync(movement, ct);
        var receiptLines = new List<PostingLineDto>();
        if (movement.TotalCost > 0) receiptLines.Add(new(AccountType.Inventory, movement.TotalCost, 0));
        if (correction != 0) receiptLines.Add(new(AccountType.CostOfSales, Math.Max(correction, 0), Math.Max(-correction, 0)));
        if (value > 0) receiptLines.Add(new(opening ? AccountType.OpeningBalances : AccountType.GoodsReceivedNotInvoiced, 0, value));
        if (receiptLines.Count > 0)
            await posting.PostAsync(new(input.OperationId, LedgerEntryKind.GoodsReceipt, AccountingReferenceType.InventoryReceipt,
                input.OperationId, DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, clock.LocalTimeZone).DateTime),
                opening ? AccountingDescriptions.InventoryOpening : AccountingDescriptions.GoodsReceipt,
                receiptLines), ct);
    }

    private static decimal ReceiptQuantity(Product product, ReceiveStockDto input)
    {
        if (!input.ReceiveInPackages)
        {
            return input.Quantity;
        }
        if (product.UnitOfMeasureId is null || product.PackagingUnitId is null || product.UnitsPerPackage is not > 0)
        {
            Fail("ReceiveInPackages", "ProductHasNoPackaging");
        }
        return input.Quantity * product.UnitsPerPackage.Value;
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

    private async Task ReceiveBatchAsync(ReceiveStockDto input, decimal quantity, decimal cost, CancellationToken ct)
    {
        var batch = await GetOrCreateBatch(input.ProductId, input.WarehouseId, input.BatchNumber!.Trim(), input.ExpiryDate, ct);
        EnsureCapacity(batch.Quantity, quantity);
        batch.CostPrice = InventoryValuation.ReceiveCost(batch.Quantity, batch.CostPrice, quantity, cost);
        batch.Quantity += quantity;
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
        var now = clock.GetUtcNow();
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
        var value = InventoryValuation.IssueValue(source.Quantity, source.TotalCost, input.Quantity);
        if (product.HasExpiry)
        {
            await TransferBatchAsync(input, cost, DateOnly.FromDateTime(now.UtcDateTime), ct);
        }
        source.Quantity -= input.Quantity;
        source.TotalCost -= value;
        source.AverageCost = InventoryValuation.Average(source.Quantity, source.TotalCost);
        source.LastUpdated = now;
        Receive(target, input.Quantity, value, now);
        await RecordTransferAsync(input, cost, value, now, ct);
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

    private async Task RecordTransferAsync(TransferStockDto input, decimal cost, decimal value, DateTimeOffset now, CancellationToken ct)
    {
        var outgoing = Movement(input, input.SourceWarehouseId, StockMovementType.TransferOut, cost, "Transfer", now);
        var incoming = Movement(input, input.DestinationWarehouseId, StockMovementType.TransferIn, cost, "Transfer", now);
        outgoing.TotalCost = incoming.TotalCost = value;
        await movements.AddAsync(outgoing, ct);
        await movements.AddAsync(incoming, ct);
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
    private static void Receive(Stock stock, decimal quantity, decimal value, DateTimeOffset now)
    {
        if (!MoneyRules.IsValid(Math.Abs(stock.TotalCost + value))) Fail("ProductId", "ReceiptCostOutOfRange");
        stock.TotalCost += value;
        stock.Quantity += quantity;
        stock.AverageCost = InventoryValuation.Average(stock.Quantity, stock.TotalCost);
        stock.LastUpdated = now;
    }
    private static StockMovement Movement(InventoryOperationDto input, Guid warehouse, StockMovementType type, decimal cost, string reference, DateTimeOffset now) =>
        Movement(input.ProductId, input.Quantity, input.OperationId, warehouse, type, cost, reference, now,
            string.IsNullOrWhiteSpace(input.BatchNumber) ? input.Notes : "Batch: " + input.BatchNumber.Trim() + "\n" + input.Notes);

    private static StockMovement Movement(Guid product, decimal quantity, Guid referenceId, Guid warehouse,
        StockMovementType type, decimal cost, string reference, DateTimeOffset now, string? notes = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = now,
        ProductId = product,
        WarehouseId = warehouse,
        Type = type,
        Quantity = quantity,
        UnitCost = cost,
        ReferenceId = referenceId,
        ReferenceType = reference,
        Notes = notes
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
