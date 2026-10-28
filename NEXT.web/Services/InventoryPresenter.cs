using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Services;
public sealed class InventoryPresenter(IInventoryQueryService queries, IReferenceDataService references, QueryFilterPresenter filters)
{
    public async Task<TableModel> StocksAsync(StockQueryModel query, CancellationToken ct)
    {
        var result = await queries.StocksAsync(query, ct);
        query.PageNumber = result.PageNumber; query.PageSize = result.PageSize;
        return new("Stocks", "StocksDescription", ["Product", "Warehouse", "Quantity", "ReservedQuantity", "AvailableQuantity", "AverageCost"],
            result.Data.Select(x => new TableRow([x.ProductName, x.WarehouseName, x.Quantity.ToString("N3"), x.ReservedQuantity.ToString("N3"), x.AvailableQuantity.ToString("N3"), x.AverageCost.ToString("N2")],
                [new("Transfer", "/Inventory/Transfer?productId=" + x.ProductId + "&sourceWarehouseId=" + x.WarehouseId)])).ToArray(),
            new("Receive", "/Inventory/Receive"), result.PageNumber, result.TotalPages, result.TotalCount, Query: query, SortOptions: [new("Quantity", "Quantity"), new("ReservedQuantity", "ReservedQuantity"), new("AverageCost", "AverageCost"), new("CreatedAt", "CreatedAt")], FilterFields: await filters.FieldsAsync(query, ct), ActiveFilters: QueryFilterPresenter.ActiveCount(query), PageSize: result.PageSize);
    }
    public async Task<TableModel> MovementsAsync(StockMovementQueryModel query, CancellationToken ct)
    {
        var result = await queries.MovementsAsync(query, ct);
        query.PageNumber = result.PageNumber; query.PageSize = result.PageSize;
        return new("StockMovements", "StockMovementsDescription", ["Product", "Warehouse", "MovementType", "Quantity", "UnitCost", "OperationId", "CreatedAt"],
            result.Data.Select(x => new TableRow([x.ProductName, x.WarehouseName, x.Type, x.Quantity.ToString("N3"), x.UnitCost.ToString("N2"), x.ReferenceId?.ToString() ?? "—", x.CreatedAt.ToLocalTime().ToString("g")], [])).ToArray(),
            Page: result.PageNumber, TotalPages: result.TotalPages, TotalCount: result.TotalCount, Query: query, SortOptions: [new("CreatedAt", "CreatedAt"), new("Quantity", "Quantity"), new("UnitCost", "UnitCost")], FilterFields: await filters.FieldsAsync(query, ct), ActiveFilters: QueryFilterPresenter.ActiveCount(query), PageSize: result.PageSize);
    }
    public async Task<TableModel> BatchesAsync(ProductBatchQueryModel query, CancellationToken ct)
    {
        var result = await queries.BatchesAsync(query, ct);
        query.PageNumber = result.PageNumber; query.PageSize = result.PageSize;
        return new("ProductBatches", "ProductBatchesDescription", ["Product", "Warehouse", "BatchNumber", "ExpiryDate", "Quantity", "UnitCost"],
            result.Data.Select(x => new TableRow([x.ProductName, x.WarehouseName, x.BatchNumber, x.ExpiryDate?.ToString("yyyy-MM-dd") ?? "—", x.Quantity.ToString("N3"), x.CostPrice.ToString("N2")], [])).ToArray(),
            Page: result.PageNumber, TotalPages: result.TotalPages, TotalCount: result.TotalCount, Query: query, SortOptions: [new("ExpiryDate", "ExpiryDate"), new("Quantity", "Quantity"), new("CostPrice", "UnitCost")], FilterFields: await filters.FieldsAsync(query, ct), ActiveFilters: QueryFilterPresenter.ActiveCount(query), PageSize: result.PageSize);
    }
    public async Task<ModalForm> FormAsync(bool transfer, string action, Guid? productId, Guid? sourceWarehouseId, CancellationToken ct)
    {
        var products = (await references.GetAsync("Products", ct)).Select(x => new FieldOption(x.Value, x.Label)).ToArray();
        var warehouses = (await references.GetAsync("Warehouses", ct)).Select(x => new FieldOption(x.Value, x.Label)).ToArray();
        List<FormField> fields = [new("OperationId", "OperationId", Guid.NewGuid().ToString(), "hidden", true),
            new("ProductId", "Product", productId?.ToString(), "select", true, Options: products)];
        if (transfer)
        {
            fields.Add(new("SourceWarehouseId", "SourceWarehouse", sourceWarehouseId?.ToString(), "select", true, Options: warehouses));
            fields.Add(new("DestinationWarehouseId", "DestinationWarehouse", Type: "select", Required: true, Options: warehouses));
        }
        else fields.Add(new("WarehouseId", "Warehouse", Type: "select", Required: true, Options: warehouses));
        fields.Add(new("Quantity", "Quantity", Type: "number", Required: true));
        if (!transfer) fields.Add(new("UnitCost", "UnitCost", "0", "number", true));
        fields.Add(new("BatchNumber", "BatchNumber", MaxLength: 100));
        if (!transfer) fields.Add(new("ExpiryDate", "ExpiryDate", Type: "date"));
        fields.Add(new("Notes", "Notes", Type: "textarea", MaxLength: 2000));
        return new(transfer ? "Transfer" : "Receive", action, fields, Message: "InventoryBatchHint");
    }
}
