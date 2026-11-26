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

public sealed class AccountingOpenings(IAccountingStore store, IRepository<AccountOpening> openings,
    IAccountingPostingService posting, IUnitOfWork work, ICurrentUserService current, IUserQueryService users, TimeProvider clock)
{
    public Task<OpeningPosition> PreviewAsync(AccountType account, CancellationToken ct)
    {
        ValidateAccount(account);
        return work.ExecuteInTransactionAsync(async token => BoundDate(await store.ReadOpeningPositionAsync(account, false, token)), ct);
    }
    public async Task<AccountOpeningDto?> HistoryAsync(AccountType account, CancellationToken ct)
    {
        var result = await openings.FirstOrDefaultAsync(o => o.Account == account, ct);
        return result is null ? null : new(result.Date, result.BeforeBalance, result.VerifiedBalance, result.Difference, result.Reason, result.ActorName);
    }
    public Task<Guid> ApproveAsync(ApproveOpeningInput input, CancellationToken ct)
    {
        ValidateAccount(input.Account);
        RequestValidation.Identifier(input.OperationId);
        var reason = RequestValidation.Text(input.Reason, nameof(input.Reason), 500);
        if (input.Date == default || input.Date > DateOnly.FromDateTime(clock.GetLocalNow().DateTime) || !MoneyRules.IsValid(input.VerifiedBalance))
            RequestValidation.Fail("Date", "AccountingOpeningInvalid");
        return work.ExecuteInTransactionAsync(async token =>
        {
            var actor = await users.FindUserAsync(current.UserId?.ToString() ?? "", token);
            if (actor is null || !actor.IsActive) RequestValidation.Fail("UserId", "InvoiceActorUnavailable");
            var previous = await openings.FirstOrDefaultAsync(o => o.OperationId == input.OperationId, token);
            if (previous is not null)
            {
                if (previous.ActorId != actor.Id || previous.Account != input.Account || previous.Date != input.Date || previous.VerifiedBalance != input.VerifiedBalance || previous.PositionHash != input.PositionHash || previous.Reason != reason)
                    throw new ConflictException("AccountingOperationConflict");
                return previous.Id;
            }
            var position = BoundDate(await store.ReadOpeningPositionAsync(input.Account, true, token));
            if (position.Approved) throw new ConflictException("AccountingOpeningAlreadyApproved");
            if (position.Hash != input.PositionHash) throw new ConflictException("AccountingOpeningStale");
            if (position.LatestDate > input.Date) RequestValidation.Fail("Date", "AccountingOpeningDateBeforeActivity");
            if (input.Account == AccountType.Inventory && input.VerifiedBalance != position.InventoryValue)
                throw new ConflictException("AccountingOpeningStale");
            if (input.VerifiedBalance == position.BookBalance) RequestValidation.Fail("VerifiedBalance", "AccountingOpeningNoDifference");
            AccountOpening document;
            try { document = AccountOpening.Create(input.OperationId, input.Account, input.Date, position.BookBalance, input.VerifiedBalance, position.Hash, reason, actor.Id, actor.FullName); }
            catch (ArgumentException) { RequestValidation.Fail("VerifiedBalance", "AccountingOpeningInvalid"); throw; }
            await openings.AddAsync(document, token);
            var difference = document.Difference;
            await posting.PostAsync(new(input.OperationId, LedgerEntryKind.OpeningBalance, AccountingReferenceType.OpeningAdoption,
                document.Id, input.Date, AccountingDescriptions.OpeningAdoption,
                [new(input.Account, Math.Max(difference, 0), Math.Max(-difference, 0)),
                 new(AccountType.OpeningBalances, Math.Max(-difference, 0), Math.Max(difference, 0))]), token);
            return document.Id;
        }, ct);
    }
    private OpeningPosition BoundDate(OpeningPosition position)
    {
        if (position.LatestStockAt is not { } at) return position;
        var local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone).DateTime);
        return position with { LatestDate = position.LatestDate is { } entryDate && entryDate > local ? entryDate : local };
    }
    private static void ValidateAccount(AccountType account)
    {
        if (account is not (AccountType.Inventory or AccountType.Treasury or AccountType.Bank or AccountType.Wallet or AccountType.Clearing)) RequestValidation.Fail("Account", "AccountingOpeningInvalid");
    }
}
