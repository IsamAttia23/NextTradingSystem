using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public sealed class Account
{
    private Account() { }
    public Guid Id { get; private set; }
    public AccountType Type { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public DebitCreditSide NormalSide { get; private set; }
    public bool IsTechnical { get; private set; }

    public static Account Create(AccountType type)
    {
        var (code, name, side) = type switch
        {
            AccountType.Sales => ("SALES", "المبيعات", DebitCreditSide.Credit),
            AccountType.Purchases => ("PURCHASES", "المشتريات", DebitCreditSide.Debit),
            AccountType.Expenses => ("EXPENSES", "المنصرفات", DebitCreditSide.Debit),
            AccountType.Receivables => ("RECEIVABLES", "الديون", DebitCreditSide.Debit),
            AccountType.Treasury => ("TREASURY", "الخزينة", DebitCreditSide.Debit),
            AccountType.CustomerAdvances => ("CUSTOMER-ADVANCES", "مقدمات العملاء والمستحق للرد", DebitCreditSide.Credit),
            AccountType.OpeningBalances => ("OPENING-BALANCES", "مقابل الأرصدة الافتتاحية", DebitCreditSide.Credit),
            AccountType.CustomerRefunds => ("CUSTOMER-REFUNDS", "المردودات المستحقة للعملاء", DebitCreditSide.Credit),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return new() { Id = IdFor(type), Type = type, Code = code, Name = name, NormalSide = side,
            IsTechnical = type is AccountType.CustomerAdvances or AccountType.OpeningBalances };
    }

    public static Guid IdFor(AccountType type)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        return Guid.Parse("a1100000-0000-0000-0000-" + ((int)type).ToString("D12"));
    }

    public static IReadOnlyList<Account> SeedData() => Enum.GetValues<AccountType>().Select(Create).ToArray();
}
