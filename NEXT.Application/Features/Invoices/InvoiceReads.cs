using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.QueryModels;
using NEXT.Application.Validation;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Application.Features.Invoices;

public sealed class InvoiceReads(IInvoiceRepository invoices, IPaginationServices pagination, IEntityValidator<PaginationQuery> validator,
    IReadRepository<InvoiceStatusHistory> history, IQueryExecutor executor, IReadRepository<Payment> payments,
    IInvoiceStateMachine stateMachine, IReadRepository<Customer> customers)
{
    public Task<TPaginationQueryModel<InvoiceSummary>> ListAsync(InvoiceQueryModel query, CancellationToken ct)
    {
        validator.Validate(query);
        var items = invoices.Query(invoices.BuildFilterExpression(query));
        var ordered = invoices.ApplySorting(items, query.SortBy, query.SortDirection ?? NEXT.Domain.Enums.SortDirection.Descending);
        return pagination.GetPagedList(ordered, source => source.Select(invoice =>
            new InvoiceSummary(invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate,
                invoice.CustomerName ?? (invoice.Customer == null ? null : invoice.Customer.Name),
                invoice.TotalAmount, invoice.Status, invoice.PaymentStatus)), query, ct);
    }

    public async Task<InvoiceDetails> DetailsAsync(Guid id, CancellationToken ct)
    {
        RequestValidation.Identifier(id);
        var invoice = await invoices.GetDetailsAsync(id, ct) ?? throw new NotFoundException("الفاتورة غير موجودة.");
        var summary = new InvoiceSummary(invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate,
            invoice.CustomerName ?? invoice.Customer?.Name, invoice.TotalAmount, invoice.Status, invoice.PaymentStatus);
        var entries = await executor.ToListAsync(history.Query(item => item.InvoiceId == id)
            .OrderByDescending(item => item.ActionAt).ThenByDescending(item => item.Id)
            .Select(item => new InvoiceStatusEntry(item.FromStatus, item.ToStatus, item.ActionAt, item.ActorFullName, item.Reason)), ct);
        var receipts = await executor.ToListAsync(payments.Query(item => item.InvoiceId == id && item.PaymentType == PaymentType.Receipt)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => new InvoicePaymentEntry(item.PaymentNumber, item.Amount, item.PaymentMethod,
                item.PaymentDate, item.CreatedAt, item.RecordedByFullName)), ct);
        var salesInvoice = invoice.Type == InvoiceType.Sales && invoice.LegacyInvoiceTypeCode is null && invoice.ParentInvoiceId is null;
        var transitions = salesInvoice ? stateMachine.GetAllowedTransitions(invoice.Status) : Array.Empty<InvoiceStatus>();
        var normalizedName = invoice.CustomerName?.Trim().ToUpperInvariant();
        var candidates = invoice.CustomerId is null && invoice.RemainingAmount > 0 && normalizedName is not null
            ? await executor.ToListAsync(customers.Query(customer => customer.IsActive && customer.Name.ToUpper() == normalizedName)
                .OrderBy(customer => customer.Id).Select(customer => new InvoiceCustomerOption(customer.Id, customer.Name, customer.Phone)), ct)
            : Array.Empty<InvoiceCustomerOption>();
        return new(summary, invoice.CreatedByFullName ?? invoice.User?.FullName, invoice.SubTotal,
            invoice.DiscountAmount, invoice.PaidAmount, invoice.RemainingAmount, invoice.Items
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new InvoiceLineDetails(item.Product?.Name, item.Quantity, item.UnitPrice, item.TotalAmount)).ToArray())
        {
            RowVersion = invoice.RowVersion.ToArray(),
            CanConfirm = transitions.Contains(InvoiceStatus.Confirmed),
            FulfillmentTargets = transitions.Where(status => status is InvoiceStatus.Shipped or InvoiceStatus.Delivered).ToArray(),
            CanRecordPayment = salesInvoice && InvoicePaymentRules.CanReceivePayment(invoice.RemainingAmount),
            CustomerId = invoice.CustomerId, Notes = invoice.Notes,
            CustomerPhone = invoice.Customer?.Phone, DebtCustomerCandidates = candidates,
            LastActionAt = invoice.LastActionAt, History = entries, Payments = receipts
        };
    }
}
