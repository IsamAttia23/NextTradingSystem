using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.QueryModels;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Features.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Domain.Enums;
using NEXT.web.Models;
using NEXT.web.Services;

namespace NEXT.web.Controllers;

public sealed class FinancialAccountsController(IAccountingReadService reads, FinancialAccountsPresenter presenter, AccountingOpenings openings, AccountingReconciliation reconciliation, TimeProvider clock) : WorkspaceController
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
    public async Task<IActionResult> Details(AccountType account, [FromQuery] AccountingQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!Enum.IsDefined(account)) throw new NotFoundException("AccountingEntryNotFound");
        query.Account = account;
        var summary = (await reads.GetAccountsAsync(query.From, query.To, ct)).Single(a => a.Type == account);
        var position = account is AccountType.Inventory or AccountType.Treasury ? await openings.PreviewAsync(account, ct) : null;
        return View(new FinancialAccountDetailsView(summary, await presenter.MovementsAsync(query, Route, ct),
            await openings.HistoryAsync(account, ct), position is { Approved: false } && (account == AccountType.Treasury || position.InventoryValue != position.BookBalance)));
    }

    [HttpGet]
    public async Task<IActionResult> ApproveOpening(AccountType account, CancellationToken ct) =>
        PartialView("_ApproveOpening", new ApproveOpeningView(await openings.PreviewAsync(account, ct), DateOnly.FromDateTime(clock.GetLocalNow().DateTime)));

    [HttpPost]
    public async Task<IActionResult> ApproveOpening(ApproveOpeningInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await openings.ApproveAsync(input, ct);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Reconciliation(CancellationToken ct) => View(await reconciliation.ReadAsync(ct));

    [HttpGet]
    public IActionResult Treasury() => RedirectToAction(nameof(Movements), new { Account = AccountType.Treasury });

    [HttpGet]
    public IActionResult Refunds() => RedirectToAction(nameof(Movements), new { Account = AccountType.CustomerRefunds });
}
