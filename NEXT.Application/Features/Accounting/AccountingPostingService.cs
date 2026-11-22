using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Accounting;

public sealed class AccountingPostingService(IAccountingStore store, IUnitOfWork unitOfWork,
    ICurrentUserService currentUser, IUserQueryService users, TimeProvider clock,
    AccountingPostingValidator postingValidator) : IAccountingPostingService
{
    public Task<Guid> PostAsync(PostingRequestDto input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(input.OperationId, nameof(input.OperationId));
        RequestValidation.Identifier(input.ReferenceId, nameof(input.ReferenceId));
        if (input.Lines is not { Count: >= 2 and <= 1000 } || input.Lines.Any(line => line is null))
            RequestValidation.Fail(nameof(input.Lines), "AccountingLinesInvalid");
        var normalized = input with { Description = RequestValidation.Text(input.Description, nameof(input.Description), 500),
            Lines = input.Lines.Select(line => line with { Description = string.IsNullOrWhiteSpace(line.Description)
                ? null : RequestValidation.Text(line.Description, nameof(line.Description), 500) }).ToArray() };

        return unitOfWork.ExecuteReadCommittedAsync(async token =>
        {
            var actor = await users.FindUserAsync(currentUser.UserId?.ToString() ?? "", token);
            if (actor is null || !actor.IsActive) RequestValidation.Fail("UserId", "InvoiceActorUnavailable");
            var entry = Build(normalized, actor.Id, RequestValidation.Text(actor.FullName, "ActorName", 200));
            await store.LockPostingAsync(entry.OperationId, entry.PostingKey, token);
            var previous = await store.FindOperationAsync(entry.OperationId, token);
            if (previous is not null)
            {
                if (previous.PostedAt is null || previous.RequestHash != entry.RequestHash || previous.ActorId != actor.Id)
                    throw new ConflictException("AccountingOperationConflict");
                return previous.Id;
            }
            if (await store.FindSourceAsync(entry.PostingKey, token) is not null)
                throw new ConflictException("AccountingSourceAlreadyPosted");
            await postingValidator.ValidateAsync(entry, token);
            await store.AppendAsync(entry, clock.GetUtcNow(), token);
            return entry.Id;
        }, ct);
    }

    private static LedgerEntry Build(PostingRequestDto input, Guid actorId, string actorName)
    {
        try
        {
            var id = Guid.NewGuid();
            var lines = input.Lines.Select((line, index) => AccountTransaction.Create(id, (short)(index + 1),
                line.Account, line.Debit, line.Credit, line.CustomerId, line.DebtItemId, line.Description)).ToArray();
            return LedgerEntry.Create(id, input.OperationId, input.Kind, input.ReferenceType, input.ReferenceId,
                input.InvoiceId, input.PaymentId, input.Date, input.Description, actorId, actorName,
                PostingFingerprint.Of(input), lines, input.ReversesEntryId);
        }
        catch (ArgumentException error)
        {
            RequestValidation.Fail(nameof(input.Lines), error is ArgumentOutOfRangeException
                ? "AccountingInvalidReference" : error.Message);
            throw;
        }
    }
}
