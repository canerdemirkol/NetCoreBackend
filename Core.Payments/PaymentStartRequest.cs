namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// One payment to start. <paramref name="CallbackUrl"/> receives the provider's server notification, <paramref name="ReturnUrl"/> the
/// browser; both come from the application's configuration, never from the request.
/// </summary>
public sealed record PaymentStartRequest(
    string OrderNo, Money Amount, PaymentBuyer Buyer, string CallbackUrl, string ReturnUrl, string Description, int MaxInstallment = 1)
{
    /// <summary>The customer agreed to keep the card for later charges. iyzico keeps it when the customer ticks it on its page.</summary>
    public bool StoreCard { get; init; }

    /// <summary>The provider's key for the customer's stored cards from an earlier payment (iyzico cardUserKey), so cards stay together.</summary>
    public string? StoredCardOwnerKey { get; init; }
}
