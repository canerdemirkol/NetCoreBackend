namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>E13-14b: a cancel or refund of a simulated payment, for the daily report.</summary>
internal sealed record SimulatedEvent(ProviderTransactionKind Kind, long AmountMinor, DateTime AtUtc);
