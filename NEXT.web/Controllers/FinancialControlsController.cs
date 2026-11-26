using Microsoft.AspNetCore.Mvc;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Features.Accounting;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
using NEXT.web.Models;
namespace NEXT.web.Controllers;
public sealed class FinancialControlsController(FinancialControls controls, TimeProvider clock) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Periods(CancellationToken ct) => View(new FinancialPeriodsView(await controls.PeriodsAsync(ct), await controls.EventsAsync(ct)));
    [HttpGet] public IActionResult CreatePeriod() => PartialView("_CreatePeriod", new CreatePeriodInput { Start = Today(), End = Today() });
    [HttpPost] public async Task<IActionResult> CreatePeriod(CreatePeriodInput input, CancellationToken ct) {
        if (!ModelState.IsValid) return ValidationProblem(ModelState); await controls.CreatePeriodAsync(input,ct); return Ok(); }
    [HttpGet] public async Task<IActionResult> ChangePeriod(Guid id, CancellationToken ct) {
        var p=(await controls.PeriodsAsync(ct)).Single(p=>p.Id==id);
        return PartialView("_ChangePeriod",new ChangePeriodInput { Id=id, Version=p.Version, Close=!p.IsClosed }); }
    [HttpPost] public async Task<IActionResult> ChangePeriod(ChangePeriodInput input,CancellationToken ct) {
        if (!ModelState.IsValid) return ValidationProblem(ModelState); await controls.ChangePeriodAsync(input,ct); return Ok(); }
    [HttpGet] public async Task<IActionResult> Transfers(CancellationToken ct) => View(await controls.TransfersAsync(ct));
    [HttpGet] public IActionResult Transfer() => PartialView("_Transfer",new CashTransferInput { OperationId=Guid.NewGuid(), Date=Today(), FromAccount=AccountType.Treasury, ToAccount=AccountType.Bank });
    [HttpPost] public async Task<IActionResult> Transfer(CashTransferInput input,CancellationToken ct) {
        if (!ModelState.IsValid) return ValidationProblem(ModelState); await controls.TransferAsync(input,ct); return Ok(); }
    [HttpGet] public async Task<IActionResult> Reconciliations(CancellationToken ct) => View(await controls.ReconciliationsAsync(ct));
    [HttpGet] public async Task<IActionResult> Reconcile(AccountType account=AccountType.Treasury,DateOnly? date=null,CancellationToken ct=default) {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var position=await controls.PositionAsync(account,date ?? Today(),ct);
        return PartialView("_Reconcile",position); }
    [HttpPost] public async Task<IActionResult> Reconcile(CashReconciliationInput input,CancellationToken ct) {
        if (!ModelState.IsValid) return ValidationProblem(ModelState); await controls.ReconcileAsync(input,ct); return Ok(); }
    private DateOnly Today()=>DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
