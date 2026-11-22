using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.QueryModels;
using NEXT.Domain.Enums;
using NEXT.web.Models;
using NEXT.web.Services;

namespace NEXT.web.Controllers;

public sealed class FinancialAccountsController(IAccountingReadService reads, FinancialAccountsPresenter presenter) : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return View(new FinancialAccountsView(await reads.GetAccountsAsync(from, to, ct), from, to));
    }

    [HttpGet]
    public async Task<IActionResult> Movements([FromQuery] AccountingQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Table(await presenter.MovementsAsync(query, Route, ct));
    }

    [HttpGet]
    public IActionResult Treasury() => RedirectToAction(nameof(Movements), new { Account = AccountType.Treasury });
}
