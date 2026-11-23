namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

internal sealed record GatewayStart(string OrderNo, long AmountMinor, string Currency, string CallbackUrl, string ReturnUrl, string Flow,
    string Description, string BuyerName, string BuyerEmail);
