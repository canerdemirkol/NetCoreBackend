namespace NetCoreBackend.NArchitecture.Core.Payments.Orders;

/// <summary>What the order should look like: its number, the amount the server computed and the merchant account it was started with.</summary>
public sealed record PaymentExpectation(string OrderNo, Money Amount, string MerchantId);
