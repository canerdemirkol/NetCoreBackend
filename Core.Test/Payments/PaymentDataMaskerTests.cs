using NetCoreBackend.NArchitecture.Core.Payments.Masking;

namespace Core.Test.Payments;

/// <summary>Card numbers never reach the log, secrets are starred, personal data is kept apart.</summary>
public class PaymentDataMaskerTests
{
    [Theory]
    [InlineData("4111111111111111", true)]       // Luhn-valid
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111111111111112", false)]      // fails Luhn: an order number, not a card
    [InlineData("123456", false)]
    public void ACardNumber_IsRecognised(string value, bool isCard)
    {
        Assert.Equal(isCard, PaymentDataMasker.IsCardNumber(value));
    }

    [Fact]
    public void AJsonBody_LosesCards_StarsSecrets_AndSeparatesPersonalData()
    {
        const string body = """{"apiKey":"k-1","order":{"cardNumber":"4111111111111111","hash":"abc"},"buyer":{"email":"a@b.c","name":"Ayşe"},"price":"100.00"}""";

        MaskedPayload masked = PaymentDataMasker.Mask(body, "application/json");

        Assert.DoesNotContain("4111111111111111", masked.Body);
        Assert.DoesNotContain("k-1", masked.Body);
        Assert.DoesNotContain("abc", masked.Body);
        Assert.DoesNotContain("a@b.c", masked.Body);
        Assert.Contains("100.00", masked.Body);
        Assert.Equal("a@b.c", masked.PersonalData["buyer.email"]);
        Assert.Equal("Ayşe", masked.PersonalData["buyer.name"]);
    }

    [Fact]
    public void AFormBody_IsMaskedTheSameWay()
    {
        MaskedPayload masked = PaymentDataMasker.Mask("merchant_oid=ORD-1&hash=xyz&email=a%40b.c&note=4111111111111111", "application/x-www-form-urlencoded");

        Assert.Contains("merchant_oid=ORD-1", masked.Body);
        Assert.DoesNotContain("xyz", masked.Body);
        Assert.DoesNotContain("4111111111111111", masked.Body);
        Assert.Equal("a@b.c", masked.PersonalData["email"]);
    }

    [Fact]
    public void ACardNumberInFreeText_IsRemovedToo()
    {
        MaskedPayload masked = PaymentDataMasker.Mask("error: card 4111-1111-1111-1111 declined", "text/plain");

        Assert.DoesNotContain("1111-1111-1111", masked.Body);
        Assert.Contains("declined", masked.Body);
    }
}
