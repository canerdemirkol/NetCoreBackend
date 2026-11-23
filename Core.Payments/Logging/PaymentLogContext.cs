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
