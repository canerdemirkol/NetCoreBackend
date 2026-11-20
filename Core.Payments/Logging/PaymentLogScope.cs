namespace NetCoreBackend.NArchitecture.Core.Payments.Logging;

/// <summary>
/// Who and what a provider call is for: the order, the step, the POS. <see cref="Step"/> labels the request row and <see cref="ResponseStep"/>
/// (or the same step) the response row. <see cref="SignatureHeaderNames"/> are logged by presence only, never by value.
/// </summary>
public sealed record PaymentLogContext
{
    public Guid? OrderId { get; init; }
    public int AttemptNo { get; init; } = 1;
    public string? CorrelationId { get; init; }
    public string? IdempotencyKey { get; init; }
    public PaymentLogStep Step { get; init; }
    public PaymentLogStep? ResponseStep { get; init; }
    public Guid? PayerTenantId { get; init; }
    public Guid? OwnerTenantId { get; init; }
    public Guid? UserId { get; init; }
    public Guid? ImpersonatorUserId { get; init; }
    public string? ClientIp { get; init; }
    public PaymentProviderName? Provider { get; init; }
    public PaymentEnvironment? Environment { get; init; }
    public Guid? MerchantAccountId { get; init; }
    public int? CredentialVersion { get; init; }
    public string? CredentialFingerprint { get; init; }
    public IReadOnlyList<string> SignatureHeaderNames { get; init; } = [];
}

/// <summary>
/// The ambient <see cref="PaymentLogContext"/> of the current async flow. Provider calls made without one are refused by
/// <see cref="PaymentLoggingHandler"/>: there is no unlogged payment call.
/// </summary>
public static class PaymentLogScope
{
    private static readonly AsyncLocal<PaymentLogContext?> Ambient = new();

    public static PaymentLogContext? Current => Ambient.Value;

    public static IDisposable Begin(PaymentLogContext context)
    {
        PaymentLogContext? previous = Ambient.Value;
        Ambient.Value = context;
        return new Restore(previous);
    }

    private sealed class Restore(PaymentLogContext? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}
