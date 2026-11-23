using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Orders;

namespace Core.Test.Payments;

public class PaymentOrderTransitionTests
{
    private static readonly PaymentExpectation Expected = new("ORD-1", Money.Of(100m), "M-1");

    private static NotificationVerification Paid(decimal amount = 100m, string currency = "TRY", string orderNo = "ORD-1", string merchant = "M-1",
        bool signature = true) =>
        new(signature, orderNo, merchant, PaymentResultKind.Paid, Money.Of(amount, currency), "P-1", "T-1", null, "4242", "Visa", 1, "OK");

    [Theory]
    [InlineData(PaymentOrderStatus.Pending, PaymentOrderStatus.Paid, true)]
    [InlineData(PaymentOrderStatus.Pending, PaymentOrderStatus.Abandoned, true)]
    [InlineData(PaymentOrderStatus.Failed, PaymentOrderStatus.Paid, true)]      // a late success: the money was taken
    [InlineData(PaymentOrderStatus.Abandoned, PaymentOrderStatus.Paid, true)]
    [InlineData(PaymentOrderStatus.Paid, PaymentOrderStatus.Cancelled, true)]
    [InlineData(PaymentOrderStatus.Paid, PaymentOrderStatus.PartiallyRefunded, true)]
    [InlineData(PaymentOrderStatus.PartiallyRefunded, PaymentOrderStatus.Refunded, true)]
    [InlineData(PaymentOrderStatus.Paid, PaymentOrderStatus.Disputed, true)]
    [InlineData(PaymentOrderStatus.Review, PaymentOrderStatus.Paid, true)]
    [InlineData(PaymentOrderStatus.Paid, PaymentOrderStatus.Pending, false)]
    [InlineData(PaymentOrderStatus.Refunded, PaymentOrderStatus.Paid, false)]
    [InlineData(PaymentOrderStatus.Cancelled, PaymentOrderStatus.Refunded, false)]
    [InlineData(PaymentOrderStatus.Pending, PaymentOrderStatus.Refunded, false)]
    public void OnlyTheTable_MovesAnOrder(PaymentOrderStatus from, PaymentOrderStatus to, bool allowed)
    {
        Assert.Equal(allowed, PaymentOrderTransitions.CanTransition(from, to));
    }

    [Fact]
    public void AValidPaidNotification_ThatMatches_PaysThePendingOrder()
    {
        TransitionDecision decision = PaymentOrderTransitions.Decide(PaymentOrderStatus.Pending, Paid(), Expected);

        Assert.Equal(TransitionOutcome.Applied, decision.Outcome);
        Assert.Equal(PaymentOrderStatus.Paid, decision.Target);
        Assert.Empty(decision.Mismatches);
    }

    [Theory]
    [InlineData(101, "TRY", "ORD-1", "M-1", PaymentMismatch.Amount)]
    [InlineData(100, "USD", "ORD-1", "M-1", PaymentMismatch.Currency)]
    [InlineData(100, "TRY", "ORD-2", "M-1", PaymentMismatch.OrderNo)]
    [InlineData(100, "TRY", "ORD-1", "M-2", PaymentMismatch.Merchant)]
    public void AnyMismatch_SendsTheOrderToReview(decimal amount, string currency, string orderNo, string merchant, PaymentMismatch mismatch)
    {
        TransitionDecision decision = PaymentOrderTransitions.Decide(PaymentOrderStatus.Pending, Paid(amount, currency, orderNo, merchant), Expected);

        Assert.Equal(PaymentOrderStatus.Review, decision.Target);
        Assert.Equal([mismatch], decision.Mismatches);
    }

    [Fact]
    public void ALateSuccess_AfterFailedOrAbandoned_Pays()
    {
        Assert.Equal(PaymentOrderStatus.Paid, PaymentOrderTransitions.Decide(PaymentOrderStatus.Failed, Paid(), Expected).Target);
        Assert.Equal(PaymentOrderStatus.Paid, PaymentOrderTransitions.Decide(PaymentOrderStatus.Abandoned, Paid(), Expected).Target);
    }

    [Fact]
    public void ARepeatedNotification_HasNoEffect()
    {
        TransitionDecision decision = PaymentOrderTransitions.Decide(PaymentOrderStatus.Paid, Paid(), Expected);

        Assert.Equal(TransitionOutcome.NoEffect, decision.Outcome);
        Assert.Null(decision.Target);
    }

    [Fact]
    public void ABadSignature_IsRejected_WhateverItSays()
    {
        TransitionDecision decision = PaymentOrderTransitions.Decide(PaymentOrderStatus.Pending, Paid(signature: false), Expected);

        Assert.Equal(TransitionOutcome.Rejected, decision.Outcome);
        Assert.Null(decision.Target);
    }

    [Fact]
    public void AFailedNotification_FailsOnlyAPendingOrder()
    {
        NotificationVerification failed = Paid() with { Result = PaymentResultKind.Failed, ErrorCode = "card_declined" };

        Assert.Equal(PaymentOrderStatus.Failed, PaymentOrderTransitions.Decide(PaymentOrderStatus.Pending, failed, Expected).Target);
        Assert.Equal(TransitionOutcome.NoEffect, PaymentOrderTransitions.Decide(PaymentOrderStatus.Paid, failed, Expected).Outcome);
    }
}
