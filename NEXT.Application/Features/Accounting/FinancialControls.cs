using Microsoft.Extensions.Configuration;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
namespace NEXT.Application.Features.Accounting;
public sealed class FinancialControls(IFinancialControlStore store, IUnitOfWork work, IAccountingPostingService posting,
    ICurrentUserService current, IUserQueryService users, TimeProvider clock, IConfiguration config)
{
    public async Task<IReadOnlyList<FinancialPeriodView>> PeriodsAsync(CancellationToken ct) => (await store.PeriodsAsync(ct)).Select(p => new FinancialPeriodView(p.Id,p.Name,p.Start,p.End,p.IsClosed,p.Version)).ToArray();
    public Task<IReadOnlyList<PeriodEventView>> EventsAsync(CancellationToken ct) => store.EventsAsync(ct);
    public Task<IReadOnlyList<CashTransferView>> TransfersAsync(CancellationToken ct) => store.TransfersAsync(ct);
    private async Task<UserAccount> ActorAsync(bool reopen, CancellationToken ct)
    {
        var actor = await users.FindUserAsync(current.UserId?.ToString() ?? "", ct);
        if (actor is null || !actor.IsActive) throw new ConflictException("InvoiceActorUnavailable");
        var allowed = config.GetSection(reopen ? "Security:FinancialPeriodReopenRoles" : "Security:AdministratorRoles").GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).DefaultIfEmpty("Admin").ToArray();
        if (!(await users.GetUserRoles(actor, ct)).Any(role => allowed.Contains(role, StringComparer.OrdinalIgnoreCase)))
            throw new ConflictException("FinancialPermissionRequired");
        return actor;
    }
    private void Date(DateOnly date) { if (date == default || date > DateOnly.FromDateTime(clock.GetLocalNow().DateTime)) RequestValidation.Fail("Date", "AccountingPostingDateInvalid"); }
    public Task<Guid> CreatePeriodAsync(CreatePeriodInput input, CancellationToken ct) => work.ExecuteReadCommittedAsync(async token =>
    {
        var actor = await ActorAsync(false, token);
        var name = RequestValidation.Text(input.Name, "Name", 100); var reason = RequestValidation.Text(input.Reason, "Reason", 500);
        if (input.Start == default || input.End < input.Start) RequestValidation.Fail("Start", "FinancialPeriodInvalid");
        await store.LockAsync(token);
        if ((await store.PeriodsAsync(token)).Any(p => input.Start <= p.End && input.End >= p.Start)) throw new ConflictException("FinancialPeriodOverlap");
        var p = FinancialPeriod.Create(name, input.Start, input.End);
        await store.AddPeriodAsync(p, Audit(p, reason, actor), token); return p.Id;
    }, ct);
    public async Task ChangePeriodAsync(ChangePeriodInput input, CancellationToken ct) => await work.ExecuteReadCommittedAsync(async token =>
    {
        var actor = await ActorAsync(!input.Close, token); var reason = RequestValidation.Text(input.Reason, "Reason", 500);
        await store.LockAsync(token);
        var p = await store.PeriodAsync(input.Id, token) ?? throw new NotFoundException("FinancialPeriodInvalid");
        if (p.Version != input.Version || p.IsClosed == input.Close) throw new ConflictException("FinancialPeriodStale");
        if (input.Close && p.End > DateOnly.FromDateTime(clock.GetLocalNow().DateTime)) throw new ConflictException("FinancialPeriodFutureClose");
        p.Change(input.Close); await store.AddEventAsync(Audit(p, reason, actor), token); return true;
    }, ct);
    private FinancialPeriodEvent Audit(FinancialPeriod p, string reason, UserAccount actor) => new() {
        Id=Guid.NewGuid(), PeriodId=p.Id, Version=p.Version, IsClosed=p.IsClosed, Reason=reason,
        ActorId=actor.Id, ActorName=actor.FullName, CreatedAt=clock.GetUtcNow(), CreatedBy=actor.Id };
    public Task<Guid> TransferAsync(CashTransferInput input, CancellationToken ct) => work.ExecuteReadCommittedAsync(async token =>
    {
        var actor = await ActorAsync(false, token); Date(input.Date); RequestValidation.Identifier(input.OperationId);
        var reason = RequestValidation.Text(input.Reason, "Reason", 500);
        if (!CashAccounts.IsCash(input.FromAccount) || !CashAccounts.IsCash(input.ToAccount) || input.FromAccount == input.ToAccount || !MoneyRules.IsValid(input.Amount) || input.Amount <= 0)
            RequestValidation.Fail("Amount", "CashTransferInvalid");
        await store.LockAsync(token);
        var prior = await store.TransferAsync(input.OperationId, token);
        if (prior is not null) {
            if (prior.FromAccount != input.FromAccount || prior.ToAccount != input.ToAccount || prior.Date != input.Date || prior.Amount != input.Amount || prior.Reason != reason || prior.ActorId != actor.Id)
                throw new ConflictException("AccountingOperationConflict");
            return prior.Id;
        }
        await store.EnsureOpenAsync(input.Date, token);
        if ((await store.PositionAsync(input.FromAccount, input.Date, token)).Balance < input.Amount ||
            (await store.PositionAsync(input.FromAccount, DateOnly.MaxValue, token)).Balance < input.Amount)
            throw new ConflictException("CashInsufficientBalance");
        var document = new CashTransfer { Id=Guid.NewGuid(), OperationId=input.OperationId, Date=input.Date, FromAccount=input.FromAccount, ToAccount=input.ToAccount,
            Amount=input.Amount, Reason=reason, ActorId=actor.Id, ActorName=actor.FullName, CreatedBy=actor.Id };
        await store.AddTransferAsync(document, token);
        await posting.PostAsync(new(input.OperationId, LedgerEntryKind.CashTransfer, AccountingReferenceType.CashTransfer, document.Id,
            input.Date, reason, [new(input.ToAccount,input.Amount,0),new(input.FromAccount,0,input.Amount)]), token);
        return document.Id;
    }, ct);
    public Task<CashPosition> PositionAsync(AccountType account, DateOnly date, CancellationToken ct) {
        Date(date); if (!CashAccounts.IsCash(account)) RequestValidation.Fail("Account", "CashAccountInvalid");
        return store.PositionAsync(account,date,ct);
    }
    public Task<Guid> ReconcileAsync(CashReconciliationInput input, CancellationToken ct) => work.ExecuteReadCommittedAsync(async token =>
    {
        var actor=await ActorAsync(false,token); Date(input.Date); RequestValidation.Identifier(input.OperationId);
        var reason=RequestValidation.Text(input.Reason,"Reason",500); var evidence=RequestValidation.Text(input.Evidence,"Evidence",200);
        if (!CashAccounts.IsCash(input.Account) || !(input.ExternalBalance >= -MoneyRules.Maximum && input.ExternalBalance <= MoneyRules.Maximum && decimal.Round(input.ExternalBalance, 2) == input.ExternalBalance) || !MoneyRules.IsValid(input.DepositsInTransit) || !MoneyRules.IsValid(input.OutstandingPayments) ||
            (input.Account == AccountType.Treasury && (input.DepositsInTransit != 0 || input.OutstandingPayments != 0))) RequestValidation.Fail("Account","CashReconciliationInvalid");
        await store.LockAsync(token);
        var prior=await store.ReconciliationAsync(input.OperationId,token);
        if (prior is not null) {
            if (prior.ActorId != actor.Id || prior.Account != input.Account || prior.Date != input.Date || prior.ExternalBalance != input.ExternalBalance || prior.PositionHash != input.PositionHash ||
                prior.DepositsInTransit != input.DepositsInTransit || prior.OutstandingPayments != input.OutstandingPayments || prior.Evidence != evidence || prior.Reason != reason)
                throw new ConflictException("AccountingOperationConflict");
            return prior.Id;
        }
        var position=await store.PositionAsync(input.Account,input.Date,token);
        if (position.Hash != input.PositionHash) throw new ConflictException("AccountingOpeningStale");
        var document=new CashReconciliation { Id=Guid.NewGuid(), OperationId=input.OperationId, Account=input.Account, Date=input.Date,
            BookBalance=position.Balance, ExternalBalance=input.ExternalBalance, DepositsInTransit=input.DepositsInTransit, OutstandingPayments=input.OutstandingPayments,
            PositionHash=position.Hash, Evidence=evidence, Reason=reason, ActorId=actor.Id, ActorName=actor.FullName, CreatedBy=actor.Id };
        await store.AddReconciliationAsync(document,token); return document.Id;
    },ct);
    public async Task<IReadOnlyList<CashReconciliationView>> ReconciliationsAsync(CancellationToken ct)
    {
        var documents=await store.ReconciliationsAsync(ct); var result=new List<CashReconciliationView>();
        foreach (var d in documents) result.Add(new(d.Id,d.Account,d.Date,d.BookBalance,d.ExternalBalance,d.DepositsInTransit,d.OutstandingPayments,d.Difference,d.Evidence,d.Reason,d.ActorName,
            (await store.PositionAsync(d.Account,d.Date,ct)).Hash != d.PositionHash));
        return result;
    }
}
