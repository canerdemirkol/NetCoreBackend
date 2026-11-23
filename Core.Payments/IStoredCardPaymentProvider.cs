namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// A provider that keeps cards and charges them later (<see cref="PaymentCapabilities.Recurring"/>). A separate contract, so a provider
/// without stored cards (PayTR's hosted page) implements nothing it cannot do. Calls run in the caller's
/// <see cref="Logging.PaymentLogScope"/>.
/// </summary>
public interface IStoredCardPaymentProvider : IPaymentProvider
{
    /// <summary>
    /// Charges the card and answers at once. Succeeded with Paid or Failed is the provider's verdict; not Succeeded means the result is
    /// unknown (a call error), so the caller asks again with <see cref="IPaymentProvider.QueryAsync"/>.
    /// </summary>
    Task<PaymentStatusResult> ChargeStoredCardAsync(StoredCardCharge charge, MerchantCredentials credentials, CancellationToken cancellationToken);

    Task<PaymentActionResult> DeleteStoredCardAsync(StoredCardReference card, MerchantCredentials credentials, CancellationToken cancellationToken);
}
