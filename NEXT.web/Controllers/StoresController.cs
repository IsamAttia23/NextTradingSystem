using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.DTOs.Stores;
using NEXT.Application.Exceptions;
using NEXT.Application.Abstractions.Services;

namespace NEXT.web.Controllers;

[Authorize(Policy = "ManageStores")]
public sealed class StoresController(IStoreManagement stores, ICurrentUserService user) : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!stores.IsAvailable) return NotFound();
        return View(await stores.ListAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? id, CancellationToken ct)
    {
        if (!stores.IsAvailable) return NotFound();
        if (id is null) return PartialView("_Create", new CreateStoreInput { OperationId = Guid.NewGuid() });
        var store = await stores.FindAsync(id.Value, ct) ?? throw new NotFoundException("StoreUnavailable");
        return PartialView("_Create", new CreateStoreInput { OperationId = store.Id, Name = store.Name, Slug = store.Slug,
            OwnerName = store.OwnerName, OwnerUserName = store.OwnerUserName, OwnerEmail = store.OwnerEmail });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStoreInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var store = await stores.CreateAsync(input, user.UserId ?? throw new ConflictException("InvoiceActorUnavailable"), ct);
        return Ok(new { redirectUrl = Route(nameof(Index)), storeUrl = Url.Content("~/s/" + store.Slug + "/") });
    }

    [HttpPost]
    public async Task<IActionResult> Activate(StoreActivationInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await stores.SetActiveAsync(input, ct);
        return RedirectToAction(nameof(Index));
    }
}
