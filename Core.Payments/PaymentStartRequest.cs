namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// One payment to start. <paramref name="CallbackUrl"/> receives the provider's server notification, <paramref name="ReturnUrl"/> the
/// browser; both come from the application's configuration, never from the request.
/// </summary>
public sealed record PaymentStartRequest(
    string OrderNo, Money Amount, PaymentBuyer Buyer, string CallbackUrl, string ReturnUrl, string Description, int MaxInstallment = 1);
