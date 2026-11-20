namespace NetCoreBackend.NArchitecture.Core.Payments;

public enum PaymentProviderName
{
    Simulator,
    Iyzico,
    PayTr,
}

public enum PaymentEnvironment
{
    Test,
    Live,
}

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

/// <summary>
/// A merchant account's credentials, decrypted by the application for one call. <paramref name="Extra"/> carries provider-specific values
/// (a sub-merchant key, the simulator's flow).
/// </summary>
public sealed record MerchantCredentials(
    string MerchantId, string ApiKey, string SecretKey, PaymentEnvironment Environment, IReadOnlyDictionary<string, string>? Extra = null)
{
    public override string ToString() => $"MerchantCredentials {{ MerchantId = {MerchantId}, Environment = {Environment} }}";
}

/// <summary>The payer as the provider must see them (fraud checks); personal data, masked out of the log body.</summary>
public sealed record PaymentBuyer(string Id, string Name, string Email, string? Phone, string? Address, string? IdentityNumber, string Ip);

/// <summary>
/// One payment to start. <paramref name="CallbackUrl"/> receives the provider's server notification, <paramref name="ReturnUrl"/> the
/// browser; both come from the application's configuration, never from the request.
/// </summary>
public sealed record PaymentStartRequest(
    string OrderNo, Money Amount, PaymentBuyer Buyer, string CallbackUrl, string ReturnUrl, string Description, int MaxInstallment = 1);

/// <summary>
/// The provider's answer to a start: its token and the payment page (an address to open or embed, or a form's HTML), or an error. Provider
/// errors are results, not exceptions.
/// </summary>
public sealed record PaymentStartResult(
    bool Succeeded, string? ProviderToken, string? PaymentPageUrl, string? PaymentPageHtml, string? ErrorCode, string? ErrorMessage)
{
    public static PaymentStartResult Failure(string code, string? message) => new(false, null, null, null, code, message);
}

/// <summary>A notification or a browser return as it arrived: its fields and the allow-listed headers.</summary>
public sealed record IncomingPaymentMessage(IReadOnlyDictionary<string, string> Fields, IReadOnlyDictionary<string, string> Headers);

public enum PaymentResultKind
{
    Paid,
    Failed,
}

/// <summary>
/// What a notification says once its signature has been checked. <paramref name="ResponseToProvider"/> is what the provider expects back
/// (PayTR: "OK"). Every field except the signature check comes from the message, so it is compared with the order before it is believed.
/// </summary>
public sealed record NotificationVerification(
    bool SignatureValid,
    string? OrderNo,
    string? MerchantId,
    PaymentResultKind? Result,
    Money? Amount,
    string? ProviderPaymentId,
    string? ProviderTransactionId,
    string? ErrorCode,
    string? CardLast4,
    string? CardFamily,
    int? Installment,
    string? ResponseToProvider);

/// <summary>The order's number with whatever the provider returned for it (its payment and transaction ids).</summary>
public sealed record PaymentReference(string OrderNo, string? ProviderPaymentId, string? ProviderTransactionId);

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

public sealed record PaymentStatusResult(
    bool Succeeded, ProviderPaymentState State, Money? Amount, string? ProviderPaymentId, string? ProviderTransactionId, string? ErrorCode,
    string? ErrorMessage)
{
    public static PaymentStatusResult Failure(string code, string? message) => new(false, ProviderPaymentState.Unknown, null, null, null, code, message);
}

public sealed record RefundRequest(PaymentReference Reference, Money Amount);

public sealed record PaymentActionResult(bool Succeeded, string? ProviderReference, string? ErrorCode, string? ErrorMessage)
{
    public static PaymentActionResult Failure(string code, string? message) => new(false, null, code, message);
}

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

    /// <summary>Checks the signature and reads the message; no network call.</summary>
    NotificationVerification VerifyNotification(IncomingPaymentMessage message, MerchantCredentials credentials);

    Task<PaymentStatusResult> QueryAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken);

    Task<PaymentActionResult> CancelAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken);

    Task<PaymentActionResult> RefundAsync(RefundRequest request, MerchantCredentials credentials, CancellationToken cancellationToken);
}
