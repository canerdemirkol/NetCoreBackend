namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

internal sealed record GatewayCharge(string OrderNo, long AmountMinor, string Currency, string CardUserKey, string CardToken, string Description);
