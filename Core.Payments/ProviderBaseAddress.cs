namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// A merchant account's own provider address, read from <see cref="MerchantCredentials.Extra"/> under <see cref="Key"/>; the provider
/// package's address is only the default. So a provider that moves its API needs a changed account, not a new package. Only an absolute
/// HTTPS address is taken (scheme and host; the package keeps the paths), and a test account is never pointed at the live address.
/// </summary>
public static class ProviderBaseAddress
{
    public const string Key = "BaseAddress";
    public const string InvalidCode = "invalid_base_address";

    /// <param name="liveAddress">The provider's live address, refused for a test account; null when test and live share one address.</param>
    public static bool TryResolve(MerchantCredentials credentials, Uri defaultAddress, Uri? liveAddress, out Uri? address)
    {
        address = defaultAddress;
        if (credentials.Extra?.TryGetValue(Key, out string? given) != true || string.IsNullOrWhiteSpace(given))
            return true;

        address = null;
        if (!Uri.TryCreate(given.Trim(), UriKind.Absolute, out Uri? parsed) || parsed.Scheme != Uri.UriSchemeHttps)
            return false;
        var resolved = new Uri(parsed.GetLeftPart(UriPartial.Authority) + "/");
        if (credentials.Environment == PaymentEnvironment.Test && liveAddress is not null
            && Uri.Compare(resolved, liveAddress, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
            return false;
        address = resolved;
        return true;
    }
}
