namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// A card kept at the provider (iyzico: cardUserKey and cardToken). Together the two values can charge the card, so the application keeps
/// them encrypted and the payment log stars them.
/// </summary>
public sealed record StoredCardReference(string OwnerKey, string CardToken);
