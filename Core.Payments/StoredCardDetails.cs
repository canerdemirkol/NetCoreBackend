namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>A stored card as a payment's result names it: the reference, and what the customer may be shown ("Bonus •••• 0016").</summary>
public sealed record StoredCardDetails(StoredCardReference Reference, string? Last4, string? Family, string? Association);
