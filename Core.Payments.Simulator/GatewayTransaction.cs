namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

internal sealed record GatewayTransaction(string Kind, string OrderNo, long AmountMinor, long? ChargedMinor, int? Installment, long? CommissionMinor,
    long? PayoutMinor, string Currency, DateTime AtUtc);
