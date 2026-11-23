using System.Text.Json;
using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Iyzico;
using NetCoreBackend.NArchitecture.Core.Payments.Masking;

namespace Core.Test.Payments;

/// <summary>
/// iyzico's stored card (E13-13): the Checkout Form result names the stored card, a renewal charges it without 3D (/payment/auth) and is
/// believed only with its signature, a declined card is a failed payment, and turning renewal off deletes the card. The answers are shaped
/// like the sandbox's (recorded 2026-11-23).
/// </summary>
public class IyzicoStoredCardTests
{
    private const string OrderNo = "PO20261223R3NW01";
    private const string SecretKey = "sandbox-secret-key";
    private static readonly MerchantCredentials Sandbox = new("100200", "sandbox-api-key", SecretKey, PaymentEnvironment.Test);
    private static readonly StoredCardReference Card = new("user-key-1", "card-token-1");
    private static readonly PaymentBuyer Buyer =
        new("U-1", "Ayşe", "Yılmaz", "ayse@test.local", "+905551112233", "Atatürk Cd. 1", "İstanbul", "Turkey", null, null, "88.230.1.1");

    private static (IyzicoPaymentProvider Provider, RecordingHttpHandler Http) Provider(params string[] answers)
    {
        var http = new RecordingHttpHandler();
        foreach (string answer in answers)
            http.Answer(answer);
        return (new IyzicoPaymentProvider(new HttpClient(http), new IyzicoOptions()), http);
    }

    private static StoredCardCharge Charge() => new(OrderNo, Money.Of(300m), Buyer, "Pro, aylık yenileme", Card);

    // The signature of /payment/auth: paymentId:currency:basketId:conversationId:paidPrice:price (verified in the sandbox).
    private static string Charged(string paymentId = "39088852", string paidPrice = "300")
    {
        string signature = IyzicoSignature.Compute(SecretKey, paymentId, "TRY", OrderNo, OrderNo, paidPrice, "300");
        return $$"""{"status":"success","locale":"tr","conversationId":"{{OrderNo}}","price":300,"paidPrice":{{paidPrice}},"installment":1,"paymentId":"{{paymentId}}","fraudStatus":1,"cardFamily":"Paraf","lastFourDigits":"0008","basketId":"{{OrderNo}}","currency":"TRY","itemTransactions":[{"itemId":"{{OrderNo}}","paymentTransactionId":"41061693"}],"phase":"AUTH","signature":"{{signature}}"}""";
    }

    [Fact]
    public void Iyzico_ChargesStoredCards()
    {
        (IyzicoPaymentProvider provider, _) = Provider();

        Assert.IsAssignableFrom<IStoredCardPaymentProvider>(provider);
        Assert.True(provider.Capabilities.HasFlag(PaymentCapabilities.Recurring));
    }

    [Fact]
    public async Task ARenewal_ChargesTheStoredCard_WithoutThreeD_AsASubscription()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(Charged());

        PaymentStatusResult charged = await provider.ChargeStoredCardAsync(Charge(), Sandbox, CancellationToken.None);

        var call = http.Requests.Single();
        Assert.Equal((HttpMethod.Post, "/payment/auth"), (call.Method, call.Uri.AbsolutePath));
        JsonElement body = JsonDocument.Parse(call.Body).RootElement;
        Assert.Equal(("user-key-1", "card-token-1"), (body.GetProperty("paymentCard").GetProperty("cardUserKey").GetString(),
            body.GetProperty("paymentCard").GetProperty("cardToken").GetString()));
        Assert.Equal(("SUBSCRIPTION", 1, "300", OrderNo, OrderNo), (body.GetProperty("paymentGroup").GetString(), body.GetProperty("installment").GetInt32(),
            body.GetProperty("paidPrice").GetString(), body.GetProperty("conversationId").GetString(), body.GetProperty("basketId").GetString()));
        Assert.Equal("VIRTUAL", body.GetProperty("basketItems")[0].GetProperty("itemType").GetString());
        Assert.True(charged.Succeeded);
        Assert.Equal((ProviderPaymentState.Paid, Money.Of(300m), "39088852", "41061693"),
            (charged.State, charged.Amount, charged.ProviderPaymentId, charged.ProviderTransactionId));
    }

    [Fact]
    public async Task ATamperedCharge_IsNotBelieved()
    {
        (IyzicoPaymentProvider provider, _) = Provider(Charged().Replace("\"paidPrice\":300", "\"paidPrice\":3"));

        PaymentStatusResult charged = await provider.ChargeStoredCardAsync(Charge(), Sandbox, CancellationToken.None);

        Assert.False(charged.Succeeded);
        Assert.Equal(IyzicoPaymentProvider.SignatureMismatchCode, charged.ErrorCode);
    }

    [Fact]
    public async Task ADeclinedStoredCard_IsAFailedPayment()
    {
        (IyzicoPaymentProvider provider, _) = Provider(
            """{"status":"failure","errorCode":"10051","errorMessage":"Kart limiti yetersiz, yetersiz bakiye","errorGroup":"NOT_SUFFICIENT_FUNDS","locale":"tr","conversationId":"PO20261223R3NW01","paymentId":"39088854"}""");

        PaymentStatusResult charged = await provider.ChargeStoredCardAsync(Charge(), Sandbox, CancellationToken.None);

        Assert.True(charged.Succeeded);
        Assert.Equal((ProviderPaymentState.Failed, "39088854", "10051"), (charged.State, charged.ProviderPaymentId, charged.ErrorCode));
    }

    [Fact]
    public async Task AnErrorWithoutAPayment_LeavesTheResultUnknown()
    {
        (IyzicoPaymentProvider provider, _) = Provider("""{"status":"failure","errorCode":"5111","errorMessage":"cardUserKey must be sent with cardToken"}""");

        PaymentStatusResult charged = await provider.ChargeStoredCardAsync(Charge(), Sandbox, CancellationToken.None);

        Assert.False(charged.Succeeded);
        Assert.Equal("5111", charged.ErrorCode);
    }

    [Fact]
    public async Task TurningRenewalOff_DeletesTheCardAtIyzico()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider("""{"status":"success","locale":"tr","systemTime":1}""");

        PaymentActionResult deleted = await provider.DeleteStoredCardAsync(Card, Sandbox, CancellationToken.None);

        var call = http.Requests.Single();
        JsonElement body = JsonDocument.Parse(call.Body).RootElement;
        Assert.Equal((HttpMethod.Delete, "/cardstorage/card"), (call.Method, call.Uri.AbsolutePath));
        Assert.Equal(("user-key-1", "card-token-1"), (body.GetProperty("cardUserKey").GetString(), body.GetProperty("cardToken").GetString()));
        Assert.True(deleted.Succeeded);
    }

    [Fact]
    public async Task TheCheckoutForm_IsOpenedForTheCardsOwner_WhenOneIsKnown()
    {
        const string initialized = """{"status":"success","token":"tok-1","paymentPageUrl":"https://sandbox-cpp.iyzipay.com?token=tok-1"}""";
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(initialized, initialized);
        PaymentStartRequest start = new(OrderNo, Money.Of(300m), Buyer, "https://api.test/callback", "https://api.test/return", "Pro, aylık");

        await provider.StartAsync(start with { StoredCardOwnerKey = "user-key-1" }, Sandbox, CancellationToken.None);
        await provider.StartAsync(start, Sandbox, CancellationToken.None);

        Assert.Equal("user-key-1", JsonDocument.Parse(http.Requests[0].Body).RootElement.GetProperty("cardUserKey").GetString());
        Assert.False(JsonDocument.Parse(http.Requests[1].Body).RootElement.TryGetProperty("cardUserKey", out _));
    }

    [Fact]
    public async Task ThePaidResult_NamesTheStoredCard()
    {
        string signature = IyzicoSignature.Compute(SecretKey, "SUCCESS", "12345678", "TRY", OrderNo, OrderNo, "300", "300", "tok-1");
        (IyzicoPaymentProvider provider, _) = Provider(
            $$"""{"status":"success","paymentStatus":"SUCCESS","fraudStatus":1,"paymentId":"12345678","currency":"TRY","basketId":"{{OrderNo}}","conversationId":"{{OrderNo}}","paidPrice":300,"price":300,"token":"tok-1","lastFourDigits":"0016","cardFamily":"Bonus","cardAssociation":"MASTER_CARD","cardUserKey":"user-key-1","cardToken":"card-token-9","signature":"{{signature}}"}""");

        NotificationVerification verified = await provider.VerifyNotificationAsync(
            new IncomingPaymentMessage(new Dictionary<string, string> { ["token"] = "tok-1" }, new Dictionary<string, string>()), Sandbox, CancellationToken.None);

        Assert.True(verified.SignatureValid);
        Assert.Equal(new StoredCardDetails(new StoredCardReference("user-key-1", "card-token-9"), "0016", "Bonus", "MASTER_CARD"), verified.StoredCard);
    }

    [Fact]
    public async Task AResultWithoutACardToken_NamesNoStoredCard()
    {
        string signature = IyzicoSignature.Compute(SecretKey, "SUCCESS", "12345678", "TRY", OrderNo, OrderNo, "300", "300", "tok-1");
        (IyzicoPaymentProvider provider, _) = Provider(
            $$"""{"status":"success","paymentStatus":"SUCCESS","fraudStatus":1,"paymentId":"12345678","currency":"TRY","basketId":"{{OrderNo}}","conversationId":"{{OrderNo}}","paidPrice":300,"price":300,"token":"tok-1","lastFourDigits":"0016","signature":"{{signature}}"}""");

        NotificationVerification verified = await provider.VerifyNotificationAsync(
            new IncomingPaymentMessage(new Dictionary<string, string> { ["token"] = "tok-1" }, new Dictionary<string, string>()), Sandbox, CancellationToken.None);

        Assert.Null(verified.StoredCard);
    }

    [Fact]
    public void TheCardsOwnerKey_IsASecret_InThePaymentLog()
    {
        MaskedPayload masked = PaymentDataMasker.Mask("""{"cardUserKey":"user-key-1","cardToken":"card-token-1"}""", "application/json", PaymentMaskingRules.Default);

        Assert.DoesNotContain("user-key-1", masked.Body);
        Assert.DoesNotContain("card-token-1", masked.Body);
    }
}
