using Microsoft.AspNetCore.Mvc;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Features.Accounting;
using NEXT.Application.QueryModels;
using NEXT.Domain.Enums;
using NEXT.web.Models;
using NEXT.web.Services;
namespace NEXT.web.Controllers;

public sealed class DebtBookController(DebtBookReads reads, DebtBookPayments payments, DebtBookWithdrawals withdrawals,
 TablePresenter tables, FinancialAccountsPresenter movements) : WorkspaceController
{
    private string Text(string key) => HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<NEXT.web.Localization.UiText>>()[key];
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DebtBookQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var page = await reads.ListAsync(query, ct);
        var table = await tables.BuildAsync("DebtBook", ["CustomerName", "DebtBookCycle", "DebtBookAccountStatus", "RemainingAmount"], page, query,
         a => new TableRow([a.Name, a.CycleNumber.ToString(), a.ClosedAt.HasValue ? Text("DebtBookClosed") : Text("DebtBookOpen"), a.Balance.ToString("N2") + " SDG"], [new("Details", Route(nameof(Details), new { id = a.Id }), Modal: false)]),
         [new("Name", "CustomerName"), new("Balance", "RemainingAmount"), new("LastActivity", "DebtBookLastActivity")], ct);
        return Table(table with { SectionLabel = "FinanceGroup", CssClass = "financial-debt-book", NumericColumns = ["RemainingAmount"] });
    }
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, [FromQuery] AccountingQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var statement = await reads.StatementAsync(id, ct);
        query.CustomerId = statement.Account.CustomerId;
        query.DebtAccountId = id;
        query.Account = AccountType.Receivables;
        return View(new DebtBookDetailsView(statement, (await movements.MovementsAsync(query, Route, ct)) with { Embedded = true, Title = "DebtBookTransactions" }));
    }
    [HttpGet] public async Task<IActionResult> Receive(Guid id, CancellationToken ct) => PartialView("_Receive", await reads.StatementAsync(id, ct));
    [HttpPost]
    public async Task<IActionResult> Receive(Guid id, DebtBookPaymentInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        await payments.ReceiveAsync(id, input, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id }) });
    }
    [HttpGet]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var a = await reads.AccountAsync(id, ct);
        return PartialView("~/Views/Invoices/_CreateInvoice.cshtml", new InvoiceDraftForm(new DebtBookWithdrawalInput { OperationId = Guid.NewGuid(), CustomerId = a.CustomerId, CustomerName = a.Name, CustomerPhone = a.Phone }, "Withdraw", id, a.RowVersion));
    }
    [HttpPost]
    public async Task<IActionResult> Withdraw(Guid id, DebtBookWithdrawalInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var invoice = await withdrawals.WithdrawAsync(id, input, ct);
        var updated = await reads.AccountForInvoiceAsync(invoice, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id = updated }), invoiceUrl = Url.Action("Details", "Invoices", new { id = invoice }) });
    }
}
