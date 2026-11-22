using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Invoices.Events;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Application.Features.Accounting;

public sealed class InvoiceDispatchAccountingHandler(IReadRepository<Payment> payments, IReadRepository<LedgerEntry> entries,
    IQueryExecutor executor, IAccountingStore ledger, IAccountingPostingService posting, InvoiceDebtResolver debtResolver)
    : INotificationHandler<InvoiceStatusChanged>
{
    public async Task Handle(InvoiceStatusChanged notification, CancellationToken ct)
    {
        if (notification.From != InvoiceStatus.Confirmed || notification.To is not (InvoiceStatus.Shipped or InvoiceStatus.Delivered)) return;
        var invoice = notification.Invoice;
        if (invoice.TaxAmount != 0) RequestValidation.Fail("TaxAmount", "AccountingTaxNotConfigured");
        if (!MoneyRules.IsValid(invoice.TotalAmount) || !MoneyRules.IsValid(invoice.PaidAmount) ||
            invoice.RemainingAmount < 0 || invoice.PaidAmount + invoice.RemainingAmount != invoice.TotalAmount)
            throw new ConflictException("AccountingInvoiceReviewRequired");
        var key = LedgerEntry.Key(LedgerEntryKind.SalesRecognition, AccountingReferenceType.Invoice, invoice.Id);
        if (await ledger.FindSourceAsync(key, ct) is not null) throw new ConflictException("AccountingInvoiceAlreadyRecognized");
        var receipts = await executor.ToListAsync(payments.Query(item => item.InvoiceId == invoice.Id), ct);
        if (receipts.Any(item => item.PaymentType != PaymentType.Receipt || item.Amount <= 0) || receipts.Sum(item => item.Amount) != invoice.PaidAmount)
            throw new ConflictException("AccountingInvoiceReviewRequired");
        var ids = receipts.Select(item => item.Id).ToArray();
        var reversals = entries.Query(item => item.ReversesEntryId != null);
        if (ids.Length > 0 && await entries.CountAsync(item => item.Kind == LedgerEntryKind.Receipt &&
            item.PostedAt != null && item.PaymentId.HasValue && ids.Contains(item.PaymentId.Value) &&
            !reversals.Any(reversal => reversal.ReversesEntryId == item.Id), ct) != ids.Length)
            throw new ConflictException("AccountingInvoiceReviewRequired");
        if (invoice.TotalAmount == 0) return;
        var lines = new List<PostingLineDto>();
        if (invoice.PaidAmount > 0) lines.Add(new(AccountType.CustomerAdvances, invoice.PaidAmount, 0, invoice.CustomerId));
        if (invoice.RemainingAmount > 0)
        {
            var debt = await debtResolver.ResolveAsync(invoice, notification.ActorId, notification.CustomerId,
                notification.CustomerPhone, notification.Date, ct);
            lines.Add(new(AccountType.Receivables, invoice.RemainingAmount, 0, invoice.CustomerId, debt.Id));
        }
        lines.Add(new(AccountType.Sales, 0, invoice.TotalAmount, invoice.CustomerId));
        await posting.PostAsync(new(notification.OperationId, LedgerEntryKind.SalesRecognition, AccountingReferenceType.Invoice,
            invoice.Id, notification.Date, "قيمة فاتورة مبيعات رقم " + invoice.InvoiceNumber, lines, invoice.Id), ct);
    }
}
