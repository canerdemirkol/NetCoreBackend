using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

namespace Core.Test.Payments;

/// <summary>
/// iyzico's HMAC rules (docs.iyzico.com: HMACSHA256 auth, response signature validation, webhook V3). The expected values were computed
/// independently (Python hmac) from the documented formulas, so a change in field order or format fails here.
/// </summary>
public class IyzicoSignatureTests
{
    private const string ApiKey = "sandbox-api-key";
    private const string SecretKey = "sandbox-secret-key";

    [Fact]
    public void TheAuthorizationHeader_SignsRandomKeyPathAndBody_AsTheDocsSay()
    {
        string header = IyzicoAuthorization.Header(ApiKey, SecretKey, "123456789", "/payment/bin/check",
            """{"locale":"tr","binNumber":"535805","conversationId":"docsTest-v1"}""");

        Assert.Equal("IYZWSv2 YXBpS2V5OnNhbmRib3gtYXBpLWtleSZyYW5kb21LZXk6MTIzNDU2Nzg5JnNpZ25hdHVyZToyZGJhYWUwNWFkYTk2NmYyZTE4YTZiZTQyYzQzOGNhMmViZDliMjFiYjY0YjYxZWNiYjgyNzhiNjE3MWYwODI1",
            header);
    }

    [Fact]
    public void AResponseSignature_IsTheHexHmacOfTheFieldsJoinedWithColons()
    {
        Assert.Equal("ca66b9740d106c82f3f70bdb01267bff0cfe60804bdf46f3aaeb53a55ae047af",
            IyzicoSignature.Compute(SecretKey, "SUCCESS", "12345678", "TRY", "PO20261123A7K3Q9", "PO20261123A7K3Q9", "100.5", "100.5", "tok-1"));
    }

    [Fact]
    public void AWebhookSignature_FollowsVersion3()
    {
        Assert.Equal("cbd2f97259c842ffc34b203b21dd46aea0ade93124084c4a8dc5acd71046ca05",
            IyzicoSignature.Webhook(SecretKey, "CHECKOUT_FORM_AUTH", "12345678", "tok-1", "PO20261123A7K3Q9", "SUCCESS"));
    }

    [Theory]
    [InlineData("50.00", "50")]
    [InlineData("10.50", "10.5")]
    [InlineData("1234.56", "1234.56")]
    [InlineData("0.10", "0.1")]
    public void APrice_IsWrittenWithADot_AndNoTrailingZeros(string amount, string expected)
    {
        Assert.Equal(expected, IyzicoPrice.Format(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void TheTestEnvironment_GoesOnlyToTheSandbox()
    {
        Assert.Equal("https://sandbox-api.iyzipay.com/", IyzicoEndpoints.BaseAddressFor(PaymentEnvironment.Test).ToString());
        Assert.Equal("https://api.iyzipay.com/", IyzicoEndpoints.BaseAddressFor(PaymentEnvironment.Live).ToString());
    }
}
