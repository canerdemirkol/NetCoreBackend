namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>The order's number with whatever the provider returned for it (its payment and transaction ids).</summary>
public sealed record PaymentReference(string OrderNo, string? ProviderPaymentId, string? ProviderTransactionId);
