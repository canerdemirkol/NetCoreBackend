namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>A payment taken from a stored card with no customer present (a subscription renewal): no payment page, no 3D.</summary>
public sealed record StoredCardCharge(string OrderNo, Money Amount, PaymentBuyer Buyer, string Description, StoredCardReference Card);
