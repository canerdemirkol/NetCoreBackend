namespace NetCoreBackend.NArchitecture.Core.Payments;

public sealed record PaymentStatusResult(
    bool Succeeded, ProviderPaymentState State, Money? Amount, string? ProviderPaymentId, string? ProviderTransactionId, string? ErrorCode,
    string? ErrorMessage)
{
    public static PaymentStatusResult Failure(string code, string? message) => new(false, ProviderPaymentState.Unknown, null, null, null, code, message);
}
