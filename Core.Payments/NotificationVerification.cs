namespace NetCoreBackend.NArchitecture.Core.Payments;

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
    string? ResponseToProvider)
{
    /// <summary>The card the payment stored at the provider, when it stored one.</summary>
    public StoredCardDetails? StoredCard { get; init; }
}
