namespace NetCoreBackend.NArchitecture.Core.Payments.Orders;

public enum PaymentOrderStatus
{
    Pending,
    Paid,
    Failed,
    Abandoned,
    Cancelled,
    PartiallyRefunded,
    Refunded,
    // A paid notification that did not match the order, or a second payment for the same period: a person decides.
    Review,
    Disputed,
}
