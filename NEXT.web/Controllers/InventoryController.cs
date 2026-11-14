using NEXT.web.Models;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Inventory;
using NEXT.web.Services;
namespace NEXT.web.Controllers;

public sealed class InventoryController(IInventoryService inventory, InventoryPresenter presenter) : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PaginationQuery query, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        return Table(await presenter.StocksAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Movements([FromQuery] StockMovementQueryModel query, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        return Table(await presenter.MovementsAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Batches([FromQuery] PaginationQuery query, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        return Table(await presenter.BatchesAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Receive(CancellationToken ct, Guid? productId = null) =>
        Form(await presenter.ReceiveFormAsync(Route("Receive"), ct, productId));

    [HttpPost]
    public async Task<IActionResult> Receive(ReceiveStockDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        await inventory.ReceiveAsync(input, ct);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Transfer(Guid? productId, Guid? sourceWarehouseId, CancellationToken ct) =>
        Form(await presenter.TransferFormAsync(Route("Transfer"), productId, sourceWarehouseId, ct));

    [HttpPost]
    public async Task<IActionResult> Transfer(TransferStockDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        await inventory.TransferAsync(input, ct);
        return Ok();
    }
}
