using Microsoft.AspNetCore.Mvc;
using NEXT.Application.DTOs.Settings;
using NEXT.Application.Features.Settings;
using NEXT.web.Models;

namespace NEXT.web.Controllers;

public sealed class StoreSettingsController(StoreProfileSettings settings) : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await settings.ReadAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Edit(CancellationToken ct)
    {
        var profile = await settings.ReadAsync(ct);
        return Form(new("StoreProfileSettings", Route(nameof(Edit)), [
            new("RowVersion", "RowVersion", Convert.ToBase64String(profile.RowVersion ?? []), "hidden"),
            new("Name", "StoreProfileName", profile.Name, Required: true, MaxLength: 200),
            new("Subtitle", "StoreProfileSubtitle", profile.Subtitle, MaxLength: 200),
            new("Address", "Address", profile.Address, "textarea", MaxLength: 500),
            new("Phone", "Phone", profile.Phone, "tel", MaxLength: 50),
            new("SecondaryPhone", "StoreProfileSecondaryPhone", profile.SecondaryPhone, "tel", MaxLength: 50)
        ], Message: "StoreProfileHint"));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(StoreProfileInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await settings.SaveAsync(input, ct);
        return Ok(new { redirectUrl = Route(nameof(Index)) });
    }
}
