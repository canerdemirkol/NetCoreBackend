using NetCoreBackend.NArchitecture.Core.Payments;

namespace Core.Test.Payments;

/// <summary>
/// A merchant account may carry its own provider address (the application's POS form), so a changed provider address needs no package
/// release; only HTTPS is taken, and a test account is never pointed at the live address.
/// </summary>
public class ProviderBaseAddressTests
{
    private static readonly Uri Default = new("https://sandbox-api.provider.test/");
    private static readonly Uri Live = new("https://api.provider.test/");

    private static MerchantCredentials Account(string? address, PaymentEnvironment environment = PaymentEnvironment.Test) =>
        new("M-1", "key", "secret", environment, address is null ? null : new Dictionary<string, string> { [ProviderBaseAddress.Key] = address });

    [Fact]
    public void WithoutItsOwnAddress_AnAccountUsesTheDefault()
    {
        Assert.True(ProviderBaseAddress.TryResolve(Account(null), Default, Live, out Uri? address));
        Assert.Equal(Default, address);
    }

    [Fact]
    public void AnAccountsOwnAddress_WinsOverTheDefault()
    {
        Assert.True(ProviderBaseAddress.TryResolve(Account("https://sandbox-v2.provider.test"), Default, Live, out Uri? address));
        Assert.Equal(new Uri("https://sandbox-v2.provider.test/"), address);
    }

    [Theory]
    [InlineData("http://sandbox-v2.provider.test/")]
    [InlineData("not an address")]
    [InlineData("https://api.provider.test/")]
    public void AnInsecureOrMalformedAddress_OrTheLiveOneOnATestAccount_IsRefused(string given)
    {
        Assert.False(ProviderBaseAddress.TryResolve(Account(given), Default, Live, out _));
    }

    [Fact]
    public void ALiveAccount_MayUseTheLiveAddress()
    {
        Assert.True(ProviderBaseAddress.TryResolve(Account("https://api.provider.test/", PaymentEnvironment.Live), Live, Live, out Uri? address));
        Assert.Equal(Live, address);
    }
}
