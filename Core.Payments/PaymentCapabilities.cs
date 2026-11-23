namespace NetCoreBackend.NArchitecture.Core.Payments;

[Flags]
public enum PaymentCapabilities
{
    None = 0,
    Cancel = 1,
    Refund = 2,
    PartialRefund = 4,
    StatusQuery = 8,
    Installments = 16,
    Recurring = 32,
}
