using NEXT.Domain.Enums;
namespace NEXT.Domain.Services;
public static class CashAccounts
{
    public static readonly AccountType[] All = [AccountType.Treasury, AccountType.Bank, AccountType.Wallet, AccountType.Clearing];
    public static bool IsCash(AccountType account) => All.Contains(account);
    public static AccountType Resolve(AccountType? selected, PaymentMethod method)
    {
        var account = selected ?? method switch { PaymentMethod.Cash => AccountType.Treasury,
            PaymentMethod.BankTransfer => AccountType.Bank, PaymentMethod.Wallet => AccountType.Wallet, _ => AccountType.Clearing };
        if (!IsCash(account)) throw new ArgumentException("CashAccountInvalid");
        return account;
    }
}
