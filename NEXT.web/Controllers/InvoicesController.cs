using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Features.Invoices;
using NEXT.Application.QueryModels;
using NEXT.web.Models;
using NEXT.web.Services;

namespace NEXT.web.Controllers;

public sealed class InvoicesController(InvoiceReads reads, InvoiceLookups lookups, InvoiceCreation creation, TablePresenter tables,
    InvoiceConfirmation confirmation, InvoicePaymentRecording payments, InvoiceFulfillment fulfillment,
    InvoiceEditing editing, InvoiceCancellation cancellation, InvoiceRefunds refunds)
    : WorkspaceController
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] InvoiceQueryModel query, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var page = await reads.ListAsync(query, ct);
        return Table(await tables.BuildAsync("Invoices", ["InvoiceNumber", "InvoiceDate", "CustomerName", "TotalAmount", "Status"],
            page, query, item => new TableRow(
                [item.Number, item.Date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                    item.CustomerName ?? "—", item.Total.ToString("N2", CultureInfo.CurrentCulture), item.Status.ToString()],
                [new UiAction("Details", Route(nameof(Details), new { id = item.Id }), Modal: false)]),
            [], ct, new UiAction("CreateInvoice", Route(nameof(Create)))));
    }

    [HttpGet]
    public IActionResult Create() => PartialView("_CreateInvoice", new InvoiceDraftForm(new CreateInvoiceInput { OperationId = Guid.NewGuid() }));

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var edit = await editing.GetAsync(id, ct);
        return PartialView("_CreateInvoice", new InvoiceDraftForm(edit.Input, "Edit", id, edit.Input.RowVersion, edit.Products));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, EditInvoiceInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await editing.EditAsync(id, input, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id }) });
    }

    [HttpGet]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var invoice = await reads.DetailsAsync(id, ct);
        if (!invoice.CanCancel) throw new NEXT.Application.Exceptions.ConflictException("InvoiceCancellationUnavailable");
        return PartialView("_CancelInvoice", invoice);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancelInvoiceInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await cancellation.CancelAsync(id, input, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id }) });
    }

    [HttpGet]
    public async Task<IActionResult> Refund(Guid id, CancellationToken ct)
    {
        var invoice = await reads.DetailsAsync(id, ct);
        if (invoice.RefundableAmount <= 0) throw new NEXT.Application.Exceptions.ConflictException("InvoiceRefundUnavailable");
        return PartialView("_RefundInvoice", invoice);
    }

    [HttpPost]
    public async Task<IActionResult> Refund(Guid id, RecordInvoicePaymentInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await refunds.RefundAsync(id, input, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id }) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInvoiceInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var invoice = await creation.CreateAsync(input, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id = invoice.Id }), invoiceNumber = invoice.Number });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => View(await reads.DetailsAsync(id, ct));

    [HttpGet]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        var invoice = await reads.DetailsAsync(id, ct);
        if (!invoice.CanConfirm)
            throw new NEXT.Application.Exceptions.ConflictException("InvoiceTransitionInvalid");
        return PartialView("_ConfirmInvoice", invoice);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm(Guid id, ConfirmInvoiceInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        await confirmation.ConfirmAsync(id, input, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id }) });
    }

    [HttpGet]
    public async Task<IActionResult> Customers(string? search, CancellationToken ct) =>
        Json(await lookups.CustomersAsync(search, ct));

    [HttpGet]
    public async Task<IActionResult> Fulfill(Guid id, NEXT.Domain.Enums.InvoiceStatus? targetStatus, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        var invoice = await reads.DetailsAsync(id, ct);
        var target = targetStatus ?? invoice.FulfillmentTargets.FirstOrDefault();
        if (!invoice.FulfillmentTargets.Contains(target))
            throw new NEXT.Application.Exceptions.ConflictException("InvoiceFulfillmentUnavailable");
        return PartialView("_FulfillInvoice", new InvoiceFulfillmentForm(invoice, target));
    }

    [HttpPost]
    public async Task<IActionResult> Fulfill(Guid id, InvoiceFulfillmentInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        await fulfillment.ExecuteAsync(id, input, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id }) });
    }

    [HttpGet]
    public async Task<IActionResult> RecordPayment(Guid id, CancellationToken ct)
    {
        var invoice = await reads.DetailsAsync(id, ct);
        if (!invoice.CanRecordPayment)
            throw new NEXT.Application.Exceptions.ConflictException("InvoicePaymentUnavailable");
        return PartialView("_RecordPayment", invoice);
    }

    [HttpPost]
    public async Task<IActionResult> RecordPayment(Guid id, RecordInvoicePaymentInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        await payments.RecordAsync(id, input, ct);
        return Ok(new { redirectUrl = Route(nameof(Details), new { id }) });
    }

    [HttpGet]
    public async Task<IActionResult> Products(string? search, CancellationToken ct)
    {
        var products = await lookups.ProductsAsync(search, ct);
        return Json(products.Select(item => new { item.Id, item.Name, item.Barcode, item.UnitPrice, item.AvailableQuantity }));
    }
}
