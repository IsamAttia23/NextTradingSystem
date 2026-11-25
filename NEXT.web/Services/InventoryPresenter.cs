using NEXT.web.Models;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

public sealed class InventoryPresenter(IInventoryQueryService queries, TablePresenter tables, FormFieldPresenter fields, Microsoft.Extensions.Localization.IStringLocalizer<NEXT.web.Localization.UiText> text)
{
    public async Task<TableModel> StocksAsync(PaginationQuery query, CancellationToken ct) =>
        await tables.BuildAsync("Stocks", ["Product", "Warehouse", "Quantity", "ReservedQuantity", "AvailableQuantity", "CostPrice", "AverageCost", "InventoryCarryingValue"],
            await queries.StocksAsync(query, ct), query,
            x => new TableRow([x.ProductName, x.WarehouseName, InventoryQuantityDisplay.Format(x.Quantity, x.UnitsPerPackage, x.BaseUnitName, x.PackagingUnitName, text), InventoryQuantityDisplay.Format(x.ReservedQuantity, x.UnitsPerPackage, x.BaseUnitName, x.PackagingUnitName, text), InventoryQuantityDisplay.Format(x.AvailableQuantity, x.UnitsPerPackage, x.BaseUnitName, x.PackagingUnitName, text), x.CostPrice.ToString("N2"), x.AverageCost.ToString("N2"), x.TotalCost.ToString("N2")],
                [new("Transfer", "/Inventory/Transfer?productId=" + x.ProductId + "&sourceWarehouseId=" + x.WarehouseId)]),
            [new("Quantity", "Quantity"), new("ReservedQuantity", "ReservedQuantity"), new("CreatedAt", "CreatedAt")], ct, new("Receive", "/Inventory/Receive"));

    public async Task<TableModel> MovementsAsync(StockMovementQueryModel query, CancellationToken ct) =>
        await tables.BuildAsync("StockMovements", ["Product", "Warehouse", "MovementType", "Quantity", "UnitCost", "InventoryMovementValue", "Supplier", "OperationId", "CreatedAt"],
            await queries.MovementsAsync(query, ct), query,
            x => new TableRow([x.ProductName, x.WarehouseName, x.Type, InventoryQuantityDisplay.Format(x.Quantity, null, x.BaseUnitName, null, text), x.UnitCost.ToString("N2"), x.TotalCost.ToString("N2"), x.SupplierName ?? "—", x.ReferenceId?.ToString() ?? "—", x.CreatedAt.ToLocalTime().ToString("g")], []),
            [new("CreatedAt", "CreatedAt")], ct);

    public async Task<TableModel> BatchesAsync(PaginationQuery query, CancellationToken ct) =>
        await tables.BuildAsync("ProductBatches", ["Product", "Warehouse", "BatchNumber", "ExpiryDate", "Quantity", "UnitCost"],
            await queries.BatchesAsync(query, ct), query,
            x => new TableRow([x.ProductName, x.WarehouseName, x.BatchNumber, x.ExpiryDate?.ToString("yyyy-MM-dd") ?? "—", InventoryQuantityDisplay.Format(x.Quantity, x.UnitsPerPackage, x.BaseUnitName, x.PackagingUnitName, text), x.CostPrice.ToString("N2")], []),
            [new("ExpiryDate", "ExpiryDate"), new("Quantity", "Quantity"), new("CostPrice", "UnitCost")], ct);

    public async Task<ModalForm> ReceiveFormAsync(string action, CancellationToken ct, Guid? productId = null)
    {
        var products = await queries.ReceiptProductsAsync(ct);
        List<FormField> formFields =
        [
            new("OperationId", "OperationId", Guid.NewGuid().ToString(), "hidden", true),
            new("ProductId", "Product", productId?.ToString(), "select", true,
                Options: products.Select(product => new FieldOption(product.Id.ToString(), product.Name,
                    BaseUnit: product.BaseUnit, PackagingUnit: product.PackagingUnit,
                    UnitsPerPackage: product.UnitsPerPackage)).ToArray())
        ];
        formFields.Add(new("WarehouseId", "Warehouse", Type: "select", Required: true, Options: await fields.OptionsAsync("Warehouses", ct)));
        formFields.Add(new("SupplierId", "Supplier", Type: "select", Required: true, Options: await fields.OptionsAsync("Suppliers", ct)));
        formFields.Add(new("ReceiveInPackages", "QuantityUnit", "false", "select", Options: InventoryQuantityDisplay.UnitOptions));
        formFields.Add(new("Quantity", "Quantity", Type: "number", Required: true));
        formFields.Add(new("ReceiptQuantityPreview", "ReceiptQuantityPreview", Type: "quantity-output"));
        formFields.Add(new("BatchNumber", "BatchNumber", MaxLength: 100));
        formFields.Add(new("ExpiryDate", "ExpiryDate", Type: "date"));
        formFields.Add(new("Notes", "Notes", Type: "textarea", MaxLength: 2000));
        return new("Receive", action, formFields, Message: "InventoryBatchHint");
    }

    public async Task<ModalForm> TransferFormAsync(string action, Guid? productId, Guid? sourceWarehouseId, CancellationToken ct)
    {
        var formFields = await OperationFieldsAsync(productId, ct);
        var warehouses = await fields.OptionsAsync("Warehouses", ct);
        formFields.Add(new("SourceWarehouseId", "SourceWarehouse", sourceWarehouseId?.ToString(), "select", true, Options: warehouses));
        formFields.Add(new("DestinationWarehouseId", "DestinationWarehouse", Type: "select", Required: true, Options: warehouses));
        formFields.Add(new("Quantity", "Quantity", Type: "number", Required: true));
        formFields.Add(new("BatchNumber", "BatchNumber", MaxLength: 100));
        formFields.Add(new("Notes", "Notes", Type: "textarea", MaxLength: 2000));
        return new("Transfer", action, formFields, Message: "InventoryBatchHint");
    }

    private async Task<List<FormField>> OperationFieldsAsync(Guid? productId, CancellationToken ct) =>
        [new("OperationId", "OperationId", Guid.NewGuid().ToString(), "hidden", true),
         new("ProductId", "Product", productId?.ToString(), "select", true, Options: await fields.OptionsAsync("Products", ct))];
}
