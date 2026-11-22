using NEXT.Application.Exceptions;
using NEXT.Application.Features.Invoices;
using NEXT.Domain.Enums;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceAmountsTests
{
    [Fact]
    public void CalculatesAndRoundsDiscounts()
    {
        Assert.Equal(0m, InvoiceAmounts.Discount(25m, null, 0m));
        Assert.Equal(1.25m, InvoiceAmounts.Discount(25m, DiscountType.FixedAmount, 1.25m));
        Assert.Equal(0.13m, InvoiceAmounts.Discount(2.5m, DiscountType.Percentage, 5m));
        Assert.Equal(25m, InvoiceAmounts.Discount(25m, DiscountType.Percentage, 100m));
    }

    [Fact]
    public void RejectsDiscountValuesThatExceedTheirLimits()
    {
        Assert.Throws<AppValidationException>(() => InvoiceAmounts.Discount(25m, DiscountType.FixedAmount, 25.01m));
        Assert.Throws<AppValidationException>(() => InvoiceAmounts.Discount(25m, DiscountType.Percentage, 100.01m));
        Assert.Throws<AppValidationException>(() => InvoiceAmounts.Discount(25m, DiscountType.Percentage, -1m));
        Assert.Throws<AppValidationException>(() => InvoiceAmounts.Discount(25m, null, 1m));
    }
}
