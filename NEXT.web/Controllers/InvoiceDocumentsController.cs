using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using NEXT.Application.Features.Invoices;
using NEXT.web.Services;

namespace NEXT.web.Controllers;

public sealed class InvoiceDocumentsController(InvoiceReads reads, InvoiceDocumentHtml html,
    InvoicePdfRenderer pdf) : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct) =>
        PartialView("~/Views/Invoices/_PreviewInvoice.cshtml", await reads.DetailsAsync(id, ct));

    [HttpGet]
    public async Task<IActionResult> Print(Guid id, bool autoPrint, CancellationToken ct)
    {
        ViewData["AutoPrint"] = autoPrint;
        return View("~/Views/Invoices/Print.cshtml", await reads.DetailsAsync(id, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(Guid id, bool download, CancellationToken ct)
    {
        var invoice = await reads.DetailsAsync(id, ct);
        var document = await html.RenderAsync(ControllerContext, invoice, ct);
        var bytes = await pdf.RenderAsync(document, ct);
        var number = string.Concat(invoice.Summary.Number.Take(100)
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        Response.GetTypedHeaders().ContentDisposition = new ContentDispositionHeaderValue(
            download ? "attachment" : "inline") { FileNameStar = number + ".pdf" };
        return File(bytes, "application/pdf");
    }
}
