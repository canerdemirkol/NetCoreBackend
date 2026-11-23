namespace NetCoreBackend.NArchitecture.Core.Payments.Orders;

/// <summary>
/// The order status rules, pure. <see cref="CanTransition"/> is the one table every change goes through; <see cref="Decide"/> turns a
/// verified notification into a change. A paid notification is believed only when amount, currency, order number and merchant all match the
/// order; otherwise the order goes to review (the money may have been taken, so it is not failed).
/// </summary>
public static class PaymentOrderTransitions
{
    private static readonly IReadOnlyDictionary<PaymentOrderStatus, PaymentOrderStatus[]> Allowed = new Dictionary<PaymentOrderStatus, PaymentOrderStatus[]>
    {
        [PaymentOrderStatus.Pending] = [PaymentOrderStatus.Paid, PaymentOrderStatus.Failed, PaymentOrderStatus.Abandoned, PaymentOrderStatus.Review],
        // A late success after a failure or an abandonment: the money was taken.
        [PaymentOrderStatus.Failed] = [PaymentOrderStatus.Paid, PaymentOrderStatus.Review],
        [PaymentOrderStatus.Abandoned] = [PaymentOrderStatus.Paid, PaymentOrderStatus.Review],
        [PaymentOrderStatus.Paid] =
        [
            PaymentOrderStatus.Cancelled, PaymentOrderStatus.PartiallyRefunded, PaymentOrderStatus.Refunded, PaymentOrderStatus.Disputed,
            PaymentOrderStatus.Review,
        ],
        [PaymentOrderStatus.PartiallyRefunded] = [PaymentOrderStatus.PartiallyRefunded, PaymentOrderStatus.Refunded, PaymentOrderStatus.Disputed],
        [PaymentOrderStatus.Review] =
        [
            PaymentOrderStatus.Paid, PaymentOrderStatus.Cancelled, PaymentOrderStatus.PartiallyRefunded, PaymentOrderStatus.Refunded,
            PaymentOrderStatus.Failed,
        ],
        [PaymentOrderStatus.Disputed] = [PaymentOrderStatus.Paid, PaymentOrderStatus.Refunded],
        [PaymentOrderStatus.Cancelled] = [],
        [PaymentOrderStatus.Refunded] = [],
    };

    // A paid notification changes nothing once the order is past waiting.
    private static readonly HashSet<PaymentOrderStatus> Settled =
    [
        PaymentOrderStatus.Paid, PaymentOrderStatus.PartiallyRefunded, PaymentOrderStatus.Refunded, PaymentOrderStatus.Cancelled,
        PaymentOrderStatus.Disputed, PaymentOrderStatus.Review,
    ];

    public static bool CanTransition(PaymentOrderStatus from, PaymentOrderStatus to) => Allowed[from].Contains(to);

    public static TransitionDecision Decide(PaymentOrderStatus current, NotificationVerification notification, PaymentExpectation expected)
    {
        if (!notification.SignatureValid)
            return new TransitionDecision(TransitionOutcome.Rejected, null, [], "signature invalid");

        if (notification.Result == PaymentResultKind.Failed)
            return current == PaymentOrderStatus.Pending
                ? new TransitionDecision(TransitionOutcome.Applied, PaymentOrderStatus.Failed, [], $"failed: {notification.ErrorCode}")
                : new TransitionDecision(TransitionOutcome.NoEffect, null, [], $"failure after {current}");

        if (notification.Result != PaymentResultKind.Paid)
            return new TransitionDecision(TransitionOutcome.NoEffect, null, [], "no result");

        if (Settled.Contains(current))
            return new TransitionDecision(TransitionOutcome.NoEffect, null, [], $"already {current}");

        List<PaymentMismatch> mismatches = MismatchesOf(notification, expected);
        PaymentOrderStatus target = mismatches.Count == 0 ? PaymentOrderStatus.Paid : PaymentOrderStatus.Review;
        return new TransitionDecision(TransitionOutcome.Applied, target, mismatches, mismatches.Count == 0 ? "paid" : "paid, does not match the order");
    }

    private static List<PaymentMismatch> MismatchesOf(NotificationVerification notification, PaymentExpectation expected)
    {
        var mismatches = new List<PaymentMismatch>();
        if (notification.Amount?.Currency != expected.Amount.Currency)
            mismatches.Add(PaymentMismatch.Currency);
        else if (notification.Amount.Value.Minor != expected.Amount.Minor)
            mismatches.Add(PaymentMismatch.Amount);
        if (notification.OrderNo != expected.OrderNo)
            mismatches.Add(PaymentMismatch.OrderNo);
        if (notification.MerchantId != expected.MerchantId)
            mismatches.Add(PaymentMismatch.Merchant);
        return mismatches;
    }
}
