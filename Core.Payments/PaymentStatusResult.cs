namespace NetCoreBackend.NArchitecture.Core.Payments;

public sealed record PaymentStatusResult(
    bool Succeeded, ProviderPaymentState State, Money? Amount, string? ProviderPaymentId, string? ProviderTransactionId, string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary>E13-14b: the fee paid above <see cref="Amount"/> (the order's own amount) for instalments, and their number.</summary>
    public Money? InstallmentFee { get; init; }

    public int? Installment { get; init; }

    public static PaymentStatusResult Failure(string code, string? message) => new(false, ProviderPaymentState.Unknown, null, null, null, code, message);
}
