using NEXT.Domain.Enums;

namespace NEXT.Domain.Services;

public static class InvoicePaymentRules
{
    public static bool CanReceivePayment(decimal remainingAmount) => remainingAmount > 0;

    public static PaymentStatus Status(decimal total, decimal paid)
    {
        if (total < 0 || paid < 0 || paid > total || decimal.Round(paid, 2) != paid)
            throw new ArgumentOutOfRangeException(nameof(paid), "InvoicePaymentInvalid");

        if (paid == total)
            return PaymentStatus.Paid;
        return paid == 0 ? PaymentStatus.Unpaid : PaymentStatus.Partial;
    }
}
