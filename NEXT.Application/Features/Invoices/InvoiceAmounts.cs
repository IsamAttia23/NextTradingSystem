using NEXT.Application.Validation;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Invoices;

public static class InvoiceAmounts
{
    public const decimal MaximumQuantity = 999_999_999_999.999m;

    public static decimal Money(decimal value)
    {
        var rounded = NEXT.Domain.Services.MoneyRules.Round(value);
        if (rounded is < 0 or > NEXT.Domain.Services.MoneyRules.Maximum)
            RequestValidation.Fail("TotalAmount", "مبلغ الفاتورة خارج النطاق المسموح.");
        return rounded;
    }

    public static decimal Discount(decimal subtotal, DiscountType? type, decimal value)
    {
        if (value < 0 || value != decimal.Round(value, 2) ||
            (type, value) is (null, not 0) ||
            (type == DiscountType.Percentage && value > 100) ||
            (type == DiscountType.FixedAmount && value > subtotal) ||
            (type.HasValue && !Enum.IsDefined(type.Value)))
        {
            RequestValidation.Fail("DiscountValue", "تحقق من قيمة الخصم. النسبة من صفر إلى 100 والمبلغ لا يتجاوز الإجمالي الفرعي.");
        }

        return type switch
        {
            DiscountType.Percentage => Money(subtotal * value / 100),
            DiscountType.FixedAmount => value,
            _ => 0
        };
    }
}
