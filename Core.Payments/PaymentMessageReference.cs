namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// Which order an incoming message names, read before its signature is checked (the signature needs the order's own merchant account):
/// our order number, or the provider's token when the message carries only that. Untrusted until verified.
/// </summary>
public sealed record PaymentMessageReference(string? OrderNo, string? ProviderToken);
