namespace NetCoreBackend.NArchitecture.Core.Payments.Orders;

public sealed record TransitionDecision(TransitionOutcome Outcome, PaymentOrderStatus? Target, IReadOnlyList<PaymentMismatch> Mismatches, string Reason);
