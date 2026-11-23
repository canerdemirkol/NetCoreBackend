namespace NetCoreBackend.NArchitecture.Core.Payments;

public enum ProviderPaymentState
{
    Unknown,
    Pending,
    Paid,
    Failed,
    Cancelled,
    PartiallyRefunded,
    Refunded,
}
