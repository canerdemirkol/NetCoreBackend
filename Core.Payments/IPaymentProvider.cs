namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// One payment provider (iyzico, PayTR, the simulator). Stateless: it knows no database, order or tenant; the caller passes the merchant's
/// credentials with each call and opens a <see cref="Logging.PaymentLogScope"/> so the HTTP calls are logged. Provider errors come back
/// as results; only a missing log scope or an unwritable log throws.
/// </summary>
public interface IPaymentProvider
{
    PaymentProviderName Name { get; }
    PaymentCapabilities Capabilities { get; }

    Task<PaymentStartResult> StartAsync(PaymentStartRequest request, MerchantCredentials credentials, CancellationToken cancellationToken);

    /// <summary>Reads which order a message names, without credentials and without trusting it; no network call.</summary>
    PaymentMessageReference ReferenceOf(IncomingPaymentMessage message);

    /// <summary>
    /// Checks the message and reads its result. A provider whose message carries only a token (iyzico) asks for the result here and checks
    /// the answer's signature; one whose message is signed itself (PayTR, the simulator) makes no network call. Runs in the caller's
    /// <see cref="Logging.PaymentLogScope"/>, so a call it makes is logged.
    /// </summary>
    Task<NotificationVerification> VerifyNotificationAsync(IncomingPaymentMessage message, MerchantCredentials credentials,
        CancellationToken cancellationToken);

    Task<PaymentStatusResult> QueryAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken);

    Task<PaymentActionResult> CancelAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken);

    Task<PaymentActionResult> RefundAsync(RefundRequest request, MerchantCredentials credentials, CancellationToken cancellationToken);
}
