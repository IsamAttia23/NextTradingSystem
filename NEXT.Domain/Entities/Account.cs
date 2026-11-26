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
    public AccountCategory Category { get; private set; }

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
            AccountType.Inventory => ("INVENTORY", "المخزون", DebitCreditSide.Debit),
            AccountType.CostOfSales => ("COST-OF-SALES", "تكلفة المبيعات", DebitCreditSide.Debit),
            AccountType.GoodsReceivedNotInvoiced => ("GOODS-RECEIVED", "بضاعة مستلمة غير مفوترة", DebitCreditSide.Credit),
            AccountType.Payables => ("PAYABLES", "الموردون", DebitCreditSide.Credit),
            AccountType.Bank => ("BANK", "البنك", DebitCreditSide.Debit),
            AccountType.Wallet => ("WALLET", "المحفظة", DebitCreditSide.Debit),
            AccountType.Clearing => ("CLEARING", "تحصيلات قيد التسوية", DebitCreditSide.Debit),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return new() { Id = IdFor(type), Type = type, Code = code, Name = name, NormalSide = side,
            Category = type switch {
                AccountType.Bank or AccountType.Wallet or AccountType.Clearing or AccountType.Inventory or AccountType.Receivables or AccountType.Treasury => AccountCategory.Asset,
                AccountType.CustomerAdvances or AccountType.CustomerRefunds or AccountType.GoodsReceivedNotInvoiced or AccountType.Payables => AccountCategory.Liability,
                AccountType.OpeningBalances => AccountCategory.Equity,
                AccountType.Sales => AccountCategory.Revenue,
                _ => AccountCategory.Expense },
            IsTechnical = type is AccountType.CustomerAdvances or AccountType.OpeningBalances };
    }

    public static Guid IdFor(AccountType type)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        return Guid.Parse("a1100000-0000-0000-0000-" + ((int)type).ToString("D12"));
    }

    public static IReadOnlyList<Account> SeedData() => Enum.GetValues<AccountType>().Select(Create).ToArray();
}
