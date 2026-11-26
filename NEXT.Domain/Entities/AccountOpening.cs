using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
namespace NEXT.Domain.Entities;

public sealed class AccountOpening : AuditableEntity<Guid>
{
    public Guid OperationId { get; private set; }
    public AccountType Account { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal BeforeBalance { get; private set; }
    public decimal VerifiedBalance { get; private set; }
    public decimal Difference => VerifiedBalance - BeforeBalance;
    public string PositionHash { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public Guid ActorId { get; private set; }
    public string ActorName { get; private set; } = "";
    private AccountOpening() { }
    public static AccountOpening Create(Guid operation, AccountType account, DateOnly date, decimal before, decimal verified,
        string hash, string reason, Guid actor, string actorName)
    {
        if (operation == Guid.Empty || actor == Guid.Empty || account is not (AccountType.Inventory or AccountType.Treasury or AccountType.Bank or AccountType.Wallet or AccountType.Clearing) || date == default ||
            !MoneyRules.IsValid(Math.Abs(before)) || !MoneyRules.IsValid(verified) || !MoneyRules.IsValid(Math.Abs(verified - before)) || verified == before ||
            hash.Length != 64 || string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || string.IsNullOrWhiteSpace(actorName) || actorName.Length > 200)
            throw new ArgumentException("AccountingOpeningInvalid");
        return new() { Id = operation, OperationId = operation, Account = account, Date = date,
            BeforeBalance = before, VerifiedBalance = verified, PositionHash = hash, Reason = reason,
            ActorId = actor, ActorName = actorName, CreatedBy = actor };
    }
}
