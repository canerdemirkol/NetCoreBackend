namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// E13-14b: one row of a provider's daily transaction report. <paramref name="Amount"/> is the order's amount for a payment (the fee
/// apart, in <paramref name="Charged"/>) and the amount given back for a cancel or refund. Commission and payout are what the provider
/// says it keeps and expects to pay out for a payment. <paramref name="ProviderLocalTime"/> is the provider's own clock (Turkey for iyzico
/// and PayTR); a date-only report gives midnight.
/// </summary>
public sealed record ProviderTransaction(ProviderTransactionKind Kind, string? OrderNo, string? ProviderPaymentId, string? ProviderTransactionId,
    Money Amount, Money? Charged, int? Installment, Money? Commission, Money? Payout, DateTime? ProviderLocalTime);
