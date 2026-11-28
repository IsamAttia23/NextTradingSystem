using System.Security.Cryptography;
using System.Text.Json;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Invoices;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
namespace NEXT.Application.Features.Accounting;

public sealed class DebtBookPayments(DebtBookReads reads, IInvoiceConfirmationStore invoices, IInvoiceAccountingStore customerStore,
 IRepository<DebtAccountPayment> documents, InvoicePaymentRecording payments, IUnitOfWork work, ICurrentUserService current,
 IUserQueryService users, TimeProvider clock, IReadRepository<DebtItem> debts, IQueryExecutor executor,
 IAccountingStore ledger, NEXT.Application.Abstractions.Services.IAccountingPostingService posting, IRepository<Payment> paymentSources, DebtAccountClosure closure)
{
    public Task<Guid> ReceiveAsync(Guid accountId, DebtBookPaymentInput input, CancellationToken ct) => work.ExecuteReadCommittedAsync(async token =>
    {
        RequestValidation.Identifier(input.OperationId);
        RequestValidation.Identifier(accountId);
        if (input.RowVersion is not { Length: > 0 } || !MoneyRules.IsValid(input.Amount) || input.Amount <= 0 || input.PaymentMethod is null || !Enum.IsDefined(input.PaymentMethod.Value) || input.CashAccount is null || !CashAccounts.IsCash(input.CashAccount.Value))
            RequestValidation.Fail("Amount", "DebtBookPaymentInvalid");
        if (string.IsNullOrWhiteSpace(input.Notes))
            RequestValidation.Fail("Notes", "DebtBookPaymentDescriptionRequired");
        var notes = RequestValidation.Text(input.Notes, "Notes", 500);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { accountId, input.Amount, input.PaymentMethod, input.CashAccount, notes })));
        var actor = await users.FindUserAsync(current.UserId?.ToString() ?? "", token);
        if (actor is null || !actor.IsActive)
            throw new ConflictException("InvoiceActorUnavailable");
        var existing = await documents.FirstOrDefaultAsync(d => d.Id == input.OperationId, token);
        if (existing is not null)
        { if (existing.ActorId != actor.Id || existing.RequestHash != hash) throw new ConflictException("AccountingOperationConflict"); return existing.Id; }
        var statement = await reads.StatementAsync(accountId, token);
        var targets = statement.Invoices.Where(i => i.Remaining > 0).OrderBy(i => i.Id).ToArray();
        var locked = new List<Invoice>();
        foreach (var i in targets)
            locked.Add(await invoices.LockInvoiceAsync(i.Id, token) ?? throw new ConflictException("InvoicePaymentConflict"));
        await customerStore.LockCustomerAsync(statement.Account.CustomerId, token);
        var account = await customerStore.LockDebtAccountByIdAsync(accountId, token) ?? throw new NotFoundException("DebtBookAccountNotFound");
        // A retry may have committed while we waited for the invoice/customer locks.
        existing = await documents.FirstOrDefaultAsync(d => d.Id == input.OperationId, token);
        if (existing is not null)
        { if (existing.ActorId != actor.Id || existing.RequestHash != hash) throw new ConflictException("AccountingOperationConflict"); return existing.Id; }
        if(account.ClosedAt.HasValue) throw new ConflictException("DebtBookAccountClosed");
        if (!account.RowVersion.SequenceEqual(input.RowVersion))
            throw new ConflictException("DebtBookAccountStale");
        var openingIds = (await executor.ToListAsync(debts.Query(d => d.DebtAccountId == accountId && d.Kind == DebtItemKind.OpeningBalance).OrderBy(d => d.EffectiveDate).ThenBy(d => d.Id), token)).Select(d => d.Id).ToArray();
        var opening = openingIds.Length == 0 ? Array.Empty<DebtPostingState>() : (await ledger.LockDebtsAsync(openingIds.Order().ToArray(), token)).ToArray();
        if (input.Amount > locked.Sum(i => i.RemainingAmount) + opening.Sum(d => d.Balance))
            RequestValidation.Fail("Amount", "DebtBookPaymentInvalid");
        var doc = new DebtAccountPayment
        {
            Id = input.OperationId,
            DebtAccountId = accountId,
            Number = "ACC-" + input.OperationId.ToString("N"),
            Amount = input.Amount,
            PaymentMethod = input.PaymentMethod.Value,
            CashAccount = input.CashAccount.Value,
            Date = DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
            Notes = notes,
            RequestHash = hash,
            ActorId = actor.Id,
            ActorName = actor.FullName,
            CreatedBy = actor.Id
        };
        await documents.AddAsync(doc, token);
        await work.SaveChangesAsync(token);
        var remaining = input.Amount;
        foreach (var invoice in locked.OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id))
        {
            var amount = Math.Min(remaining, invoice.RemainingAmount);
            if (amount <= 0)
                continue;
            await payments.RecordAsync(invoice.Id, new RecordInvoicePaymentInput
            {
                OperationId = Guid.NewGuid(),
                DebtAccountPaymentId = doc.Id,
                RowVersion = invoice.RowVersion,
                Amount = amount,
                CustomerId = statement.Account.CustomerId,
                PaymentMethod = input.PaymentMethod,
                CashAccount = input.CashAccount
            }, token);
            remaining -= amount;
            if (remaining == 0)
                break;
        }
        if (remaining > 0)
        {
            var amount = remaining;
            var lines = new List<PostingLineDto> { new(input.CashAccount.Value, amount, 0, account.CustomerId) };
            foreach (var id in openingIds)
            {
                var debt = opening.Single(d => d.Id == id);
                var allocated = Math.Min(remaining, Math.Max(0, debt.Balance));
                if (allocated <= 0)
                    continue;
                lines.Add(new(AccountType.Receivables, 0, allocated, account.CustomerId, id));
                remaining -= allocated;
                if (remaining == 0)
                    break;
            }
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                DebtAccountPaymentId = doc.Id,
                CustomerId = account.CustomerId,
                Amount = amount,
                PaymentMethod = doc.PaymentMethod,
                CashAccount = doc.CashAccount,
                PaymentDate = doc.Date,
                PaymentType = PaymentType.Receipt,
                Purpose = PaymentPurpose.DebtReceipt,
                CreatedBy = actor.Id,
                RecordedByFullName = actor.FullName
            };
            payment.PaymentNumber = "PAY-" + payment.Id.ToString("N");
            await paymentSources.AddAsync(payment, token);
            await posting.PostAsync(new(payment.Id, LedgerEntryKind.Receipt, AccountingReferenceType.Payment, payment.Id, doc.Date, "AccountingDescriptionDebtReceipt", lines, PaymentId: payment.Id), token);
        }
        if(!account.ClosedAt.HasValue) account.RecordActivity(clock.GetUtcNow());
        if (remaining != 0)
            throw new ConflictException("DebtBookAccountStale");
        await closure.TryCloseAsync(accountId, actor.Id, token);
        return doc.Id;
    }, ct);
}
