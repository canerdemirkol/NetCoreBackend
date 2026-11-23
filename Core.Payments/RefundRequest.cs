namespace NetCoreBackend.NArchitecture.Core.Payments;

public sealed record RefundRequest(PaymentReference Reference, Money Amount);
