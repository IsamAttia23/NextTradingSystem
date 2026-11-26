using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Invoices.Events;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Accounting;

public sealed class InvoicePaymentAccountingHandler(IAccountingStore ledger, IAccountingPostingService posting,
    IRepository<DebtItem> debts, IInvoiceAccountingStore invoiceStore, TimeProvider clock)
    : INotificationHandler<InvoicePaymentRecorded>
{
    public async Task Handle(InvoicePaymentRecorded notification, CancellationToken ct)
    {
        var invoice = notification.Invoice;
        var payment = notification.Payment;
        var recognition = await ledger.FindSourceAsync(LedgerEntry.Key(LedgerEntryKind.SalesRecognition,
            AccountingReferenceType.Invoice, invoice.Id), ct);
        if (recognition is null && invoice.Status is (InvoiceStatus.Shipped or InvoiceStatus.Delivered))
            throw new ConflictException("AccountingInvoiceReviewRequired");
        DebtItem? debt = null;
        var credit = AccountType.CustomerAdvances;
        if (recognition is not null)
        {
            debt = await debts.FirstOrDefaultAsync(item => item.InvoiceId == invoice.Id, ct)
                ?? throw new ConflictException("AccountingInvoiceDebtMissing");
            if (recognition.PostedAt is null || await ledger.HasReversalAsync(recognition.Id, ct))
                throw new ConflictException("AccountingInvoiceReviewRequired");
            credit = AccountType.Receivables;
            if (invoice.CustomerId is null) throw new ConflictException("AccountingInvoiceReviewRequired");
            var account = await invoiceStore.LockDebtAccountAsync(invoice.CustomerId.Value, ct)
                ?? throw new ConflictException("AccountingDebtUnavailable");
            if (account.Id != debt.DebtAccountId) throw new ConflictException("AccountingInvoiceReviewRequired");
            account.RecordActivity(clock.GetUtcNow());
        }
        await posting.PostAsync(new(payment.Id, LedgerEntryKind.Receipt, AccountingReferenceType.Payment, payment.Id,
            payment.PaymentDate, AccountingDescriptions.InvoiceReceipt,
            [new(payment.CashAccount, payment.Amount, 0, payment.CustomerId),
             new(credit, 0, payment.Amount, payment.CustomerId, debt?.Id)], invoice.Id, payment.Id), ct);
    }
}
