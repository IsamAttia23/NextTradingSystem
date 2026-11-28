using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Features.Products;
using NEXT.Application.QueryModels;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class ProductReportsController(ISender sender, InvoiceDocumentHtml html, InvoicePdfRenderer pdf) : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Preview([FromQuery] ProductQueryModel query, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return PartialView("_Preview", await sender.Send(new ProductReportQuery(query), ct));
    }
    [HttpGet]
    public async Task<IActionResult> Print([FromQuery] ProductQueryModel query, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return View(await sender.Send(new ProductReportQuery(query), ct));
    }
    [HttpGet]
    public async Task<IActionResult> Pdf([FromQuery] ProductQueryModel query, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var rows = await sender.Send(new ProductReportQuery(query), ct);
        var document = await html.RenderViewAsync(ControllerContext, "~/Views/ProductReports/Print.cshtml", rows, ct);
        return File(await pdf.RenderAsync(document, ct), "application/pdf", "Products.pdf");
    }
}
