namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// A merchant account's credentials, decrypted by the application for one call. <paramref name="Extra"/> carries provider-specific values
/// (a sub-merchant key, the simulator's flow).
/// </summary>
public sealed record MerchantCredentials(
    string MerchantId, string ApiKey, string SecretKey, PaymentEnvironment Environment, IReadOnlyDictionary<string, string>? Extra = null)
{
    public override string ToString() => $"MerchantCredentials {{ MerchantId = {MerchantId}, Environment = {Environment} }}";
}
