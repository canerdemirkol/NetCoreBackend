namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>One payment as the simulated provider holds it.</summary>
public sealed class SimulatedPayment
{
    public required string Token { get; init; }
    public required string OrderNo { get; init; }
    public required string MerchantId { get; init; }
    public required Money Amount { get; init; }
    public required string CallbackUrl { get; init; }
    public required string ReturnUrl { get; init; }
    public required SimulatorFlow Flow { get; init; }
    public required string Description { get; init; }
    public bool StoreCard { get; init; }
    public string? OwnerKey { get; internal set; }
    public string? StoredCardToken { get; internal set; }
    public ProviderPaymentState State { get; internal set; } = ProviderPaymentState.Pending;
    public string? PaymentId { get; internal set; }
    public string? TransactionId { get; internal set; }
    public string? CardLast4 { get; internal set; }
    public Money Refunded { get; internal set; }
    public bool DayClosed { get; internal set; }
    // A notification held back (card ending 0005) until the control sends it.
    internal IReadOnlyDictionary<string, string>? HeldNotification { get; set; }
}
