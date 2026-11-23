namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

internal sealed record GatewayReference(string OrderNo, long? AmountMinor);
