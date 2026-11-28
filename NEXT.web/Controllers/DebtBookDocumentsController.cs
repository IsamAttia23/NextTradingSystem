using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NEXT.web.Localization;
using NEXT.Application.Features.Accounting;
using NEXT.web.Services;
namespace NEXT.web.Controllers;

public sealed class DebtBookDocumentsController(DebtBookReads reads, InvoiceDocumentHtml html, InvoicePdfRenderer pdf, IStringLocalizer<UiText> text) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Preview(Guid id, CancellationToken ct) => PartialView("~/Views/DebtBook/_PreviewStatement.cshtml", await reads.StatementAsync(id, ct));
    [HttpGet] public async Task<IActionResult> Print(Guid id, bool autoPrint, CancellationToken ct) { ViewData["AutoPrint"] = autoPrint; return View("~/Views/DebtBook/Print.cshtml", await reads.StatementAsync(id, ct)); }
    [HttpGet]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var s = await reads.StatementAsync(id, ct);
        var document = await html.RenderViewAsync(ControllerContext, "~/Views/DebtBook/Print.cshtml", s, ct);
        var title = text["DebtBookStatementTitle", s.Account.Name].Value;
        var fileName = string.Concat(title.Take(140)
            .Select(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '—' ? c : '_')) + ".pdf";
        return File(await pdf.RenderAsync(document, ct), "application/pdf", fileName);
    }
}
