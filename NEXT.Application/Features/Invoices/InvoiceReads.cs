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
    IInvoiceStateMachine stateMachine, IReadRepository<Customer> customers, IReadRepository<AccountTransaction> movements,
    IReadRepository<LedgerEntry> ledger)
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
        IReadOnlyList<InvoicePaymentEntry> refunds = invoice.Status == InvoiceStatus.Cancelled
            ? await executor.ToListAsync(payments.Query(item => item.InvoiceId == id && item.Purpose == PaymentPurpose.CustomerRefund && item.PaymentType == PaymentType.Payment)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => new InvoicePaymentEntry(item.PaymentNumber, item.Amount, item.PaymentMethod, item.PaymentDate, item.CreatedAt, item.RecordedByFullName)), ct) : [];
        var refundAccount = Account.IdFor(AccountType.CustomerRefunds);
        var refundBalance = invoice.Status == InvoiceStatus.Cancelled
            ? (await executor.ToListAsync(movements.Query(line => line.AccountId == refundAccount && line.LedgerEntry.InvoiceId == id &&
                line.LedgerEntry.PostedAt != null).Select(line => new { Amount = line.Credit - line.Debit }), ct)).Sum(item => item.Amount) : 0;
        var transitions = salesInvoice ? stateMachine.GetAllowedTransitions(invoice.Status) : Array.Empty<InvoiceStatus>();
        var revenueConsistent = true;
        if (salesInvoice && invoice.Status is (InvoiceStatus.Shipped or InvoiceStatus.Delivered) && invoice.RemainingAmount > 0)
        {
            var reversals = ledger.Query(item => item.ReversesEntryId != null);
            revenueConsistent = await ledger.AnyAsync(item => item.InvoiceId == id && item.Kind == LedgerEntryKind.SalesRecognition &&
                item.PostedAt != null && !reversals.Any(reversal => reversal.ReversesEntryId == item.Id), ct);
        }
        var normalizedName = invoice.CustomerName?.Trim().ToUpperInvariant();
        var candidates = invoice.CustomerId is null && (invoice.RemainingAmount > 0 || invoice.PaidAmount > 0 || transitions.Contains(InvoiceStatus.Confirmed)) && normalizedName is not null
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
            CanEdit = salesInvoice && invoice.Status == InvoiceStatus.Pending && invoice.TaxAmount == 0,
            CanCancel = transitions.Contains(InvoiceStatus.Cancelled),
            RefundedAmount = refunds.Sum(item => item.Amount), RefundableAmount = refundBalance, Refunds = refunds,
            FulfillmentTargets = transitions.Where(status => status is InvoiceStatus.Shipped or InvoiceStatus.Delivered).ToArray(),
            CanRecordPayment = salesInvoice && revenueConsistent && invoice.Status != InvoiceStatus.Cancelled && InvoicePaymentRules.CanReceivePayment(invoice.RemainingAmount),
            CustomerId = invoice.CustomerId, Notes = invoice.Notes,
            CustomerPhone = invoice.Customer?.Phone, DebtCustomerCandidates = candidates,
            LastActionAt = invoice.LastActionAt, History = entries, Payments = receipts
        };
    }
}
