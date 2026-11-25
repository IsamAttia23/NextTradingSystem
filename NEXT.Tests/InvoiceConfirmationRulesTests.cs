using NEXT.Domain.Enums;
using NEXT.Domain.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceConfirmationRulesTests
{
    [Fact]
    public void PaymentStatusMatchesTheFinalTotalAndRejectsInvalidAmounts()
    {
        Assert.True(InvoicePaymentRules.CanReceivePayment(0.01m));
        Assert.False(InvoicePaymentRules.CanReceivePayment(0m));
        Assert.False(InvoicePaymentRules.CanReceivePayment(-1m));
        Assert.Equal(PaymentStatus.Unpaid, InvoicePaymentRules.Status(10m, 0m));
        Assert.Equal(PaymentStatus.Partial, InvoicePaymentRules.Status(10m, 3.25m));
        Assert.Equal(PaymentStatus.Paid, InvoicePaymentRules.Status(10m, 10m));
        Assert.Equal(PaymentStatus.Paid, InvoicePaymentRules.Status(0m, 0m));
        foreach (var amount in new[] { -0.01m, 10.01m, 0.001m })
            Assert.Throws<ArgumentOutOfRangeException>(() => InvoicePaymentRules.Status(10m, amount));
    }

    [Fact]
    public void OnlyPendingInvoicesCanBeConfirmed()
    {
        var rules = new InvoiceStateMachine();
        foreach (var status in Enum.GetValues<InvoiceStatus>())
        {
            Assert.Equal(status == InvoiceStatus.Pending, rules.CanTransition(status, InvoiceStatus.Confirmed));
            if (status != InvoiceStatus.Pending)
                Assert.Throws<InvalidOperationException>(() => rules.Transition(status, InvoiceStatus.Confirmed));
        }
        Assert.Equal(new[] { InvoiceStatus.Confirmed, InvoiceStatus.Cancelled }, rules.GetAllowedTransitions(InvoiceStatus.Pending));
        Assert.Equal(InvoiceStatus.Confirmed, rules.Transition(InvoiceStatus.Pending, InvoiceStatus.Confirmed));
        Assert.Equal(new[] { InvoiceStatus.Shipped, InvoiceStatus.Delivered, InvoiceStatus.Cancelled }, rules.GetAllowedTransitions(InvoiceStatus.Confirmed));
        Assert.False(rules.CanTransition(InvoiceStatus.Pending, InvoiceStatus.Pending));
    }

    [Fact]
    public void FulfillmentTransitionsMatchTheLifecycle()
    {
        var rules = new InvoiceStateMachine();
        var allowed = new[] { (InvoiceStatus.Pending, InvoiceStatus.Confirmed), (InvoiceStatus.Pending, InvoiceStatus.Cancelled), (InvoiceStatus.Confirmed, InvoiceStatus.Cancelled),
            (InvoiceStatus.Confirmed, InvoiceStatus.Shipped), (InvoiceStatus.Confirmed, InvoiceStatus.Delivered),
            (InvoiceStatus.Shipped, InvoiceStatus.Delivered) };
        foreach (var from in Enum.GetValues<InvoiceStatus>())
        {
            foreach (var to in Enum.GetValues<InvoiceStatus>())
            {
                Assert.Equal(allowed.Contains((from, to)), rules.CanTransition(from, to));
                if (allowed.Contains((from, to))) Assert.Equal(to, rules.Transition(from, to));
                else Assert.Throws<InvalidOperationException>(() => rules.Transition(from, to));
            }
        }
        Assert.Empty(rules.GetAllowedTransitions(InvoiceStatus.Delivered));
        Assert.Equal(3, (int)InvoiceStatus.Cancelled);
        Assert.Equal(4, (int)InvoiceStatus.Paid);
        Assert.Equal(5, (int)InvoiceStatus.PartiallyPaid);
    }
}
