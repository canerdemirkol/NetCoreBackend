using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

namespace Core.Test.Payments;

/// <summary>
/// iyzico's sandbox, for real: the IYZWSv2 header and the request bodies are accepted and error answers are read. A payment itself needs
/// the hosted page (card form, 3D Secure), so paid flows and their signed answers are tried by hand; these cover what needs no card.
/// iyzico refuses e-mail addresses on reserved domains such as .local, so the buyer here uses example.com.
/// </summary>
public class IyzicoSandboxTests
{
    private static MerchantCredentials Sandbox() => new("sandbox", Environment.GetEnvironmentVariable(IyzicoSandboxFactAttribute.ApiKeyVariable)!,
        Environment.GetEnvironmentVariable(IyzicoSandboxFactAttribute.SecretKeyVariable)!, PaymentEnvironment.Test);

    private static IyzicoPaymentProvider Provider() => new(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, new IyzicoOptions());

    private static string NewOrderNo() => "PO" + DateTime.UtcNow.ToString("yyyyMMdd") + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static PaymentStartRequest Start(string orderNo) => new(orderNo, Money.Of(100.5m),
        new PaymentBuyer("U-1", "Ayşe", "Yılmaz", "ayse@example.com", "+905551112233", "Atatürk Cd. 1", "İstanbul", "Turkey", "34000", null, "85.34.78.112"),
        "https://example.test/api/payments/callback/iyzico", "https://example.test/api/payments/return/iyzico", "Pro, aylık");

    [IyzicoSandboxFact]
    public async Task AStart_IsAccepted_AndGivesAPaymentPage()
    {
        PaymentStartResult started = await Provider().StartAsync(Start(NewOrderNo()), Sandbox(), CancellationToken.None);

        Assert.True(started.Succeeded, $"{started.ErrorCode}: {started.ErrorMessage}");
        Assert.False(string.IsNullOrEmpty(started.ProviderToken));
        Assert.StartsWith("https://", started.PaymentPageUrl);
    }

    // iyzico answers an unpaid form's token with an error (5122), so nothing is believed. A paid form's signed answer is tried by hand.
    [IyzicoSandboxFact]
    public async Task AnUnpaidForm_IsAnErrorAnswer_NeverReadAsPaid()
    {
        IyzicoPaymentProvider provider = Provider();
        PaymentStartResult started = await provider.StartAsync(Start(NewOrderNo()), Sandbox(), CancellationToken.None);
        Assert.True(started.Succeeded, $"{started.ErrorCode}: {started.ErrorMessage}");

        NotificationVerification verified = await provider.VerifyNotificationAsync(new IncomingPaymentMessage(
            new Dictionary<string, string> { ["token"] = started.ProviderToken! }, new Dictionary<string, string>()), Sandbox(), CancellationToken.None);

        Assert.False(verified.SignatureValid);
        Assert.Null(verified.Result);
        Assert.False(string.IsNullOrEmpty(verified.ErrorCode));
        Assert.NotEqual(IyzicoPaymentProvider.SignatureMismatchCode, verified.ErrorCode);
    }

    // A card is stored here through iyzico's card storage call with a sandbox test card (the application never sees a card number: its
    // cards are stored by the Checkout Form). Then the provider charges it as a renewal, reads a declined card, and deletes the card.
    private static async Task<StoredCardReference> StoreTestCardAsync(string cardNumber)
    {
        const string path = "/cardstorage/card";
        string body = System.Text.Json.JsonSerializer.Serialize(new
        {
            locale = "tr", conversationId = "sandbox-store", email = "ayse@example.com", externalId = "sandbox-tests",
            card = new { cardAlias = "sandbox", cardHolderName = "Ayse Yilmaz", cardNumber, expireMonth = "12", expireYear = "2030" },
        });
        MerchantCredentials sandbox = Sandbox();
        string randomKey = IyzicoAuthorization.NewRandomKey();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new IyzicoOptions().SandboxBaseAddress, path))
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(IyzicoAuthorization.RandomKeyHeader, randomKey);
        request.Headers.TryAddWithoutValidation("Authorization", IyzicoAuthorization.Header(sandbox.ApiKey, sandbox.SecretKey, randomKey, path, body));
        using var client = new HttpClient();
        using System.Text.Json.JsonDocument answer = System.Text.Json.JsonDocument.Parse(await (await client.SendAsync(request)).Content.ReadAsStringAsync());
        return new StoredCardReference(answer.RootElement.GetProperty("cardUserKey").GetString()!, answer.RootElement.GetProperty("cardToken").GetString()!);
    }

    private static StoredCardCharge Renewal(StoredCardReference card) => new(NewOrderNo(), Money.Of(100.5m), Start("unused").Buyer, "Pro, aylık yenileme", card);

    [IyzicoSandboxFact]
    public async Task AStoredCard_IsChargedAsARenewal_WithAValidSignature_ThenDeleted()
    {
        IyzicoPaymentProvider provider = Provider();
        StoredCardReference card = await StoreTestCardAsync("5528790000000008");

        PaymentStatusResult charged = await provider.ChargeStoredCardAsync(Renewal(card), Sandbox(), CancellationToken.None);
        PaymentActionResult deleted = await provider.DeleteStoredCardAsync(card, Sandbox(), CancellationToken.None);

        Assert.True(charged.Succeeded, $"{charged.ErrorCode}: {charged.ErrorMessage}");
        Assert.Equal((ProviderPaymentState.Paid, Money.Of(100.5m)), (charged.State, charged.Amount));
        Assert.False(string.IsNullOrEmpty(charged.ProviderTransactionId));
        Assert.True(deleted.Succeeded, $"{deleted.ErrorCode}: {deleted.ErrorMessage}");
    }

    [IyzicoSandboxFact]
    public async Task AStoredCardWithoutFunds_IsAFailedPayment()
    {
        IyzicoPaymentProvider provider = Provider();
        StoredCardReference card = await StoreTestCardAsync("4111111111111129");

        PaymentStatusResult charged = await provider.ChargeStoredCardAsync(Renewal(card), Sandbox(), CancellationToken.None);
        await provider.DeleteStoredCardAsync(card, Sandbox(), CancellationToken.None);

        Assert.True(charged.Succeeded, $"{charged.ErrorCode}: {charged.ErrorMessage}");
        Assert.Equal((ProviderPaymentState.Failed, "10051"), (charged.State, charged.ErrorCode));
    }

    [IyzicoSandboxFact]
    public async Task AnUnknownOrder_IsAnErrorResult()
    {
        PaymentStatusResult status = await Provider().QueryAsync(new PaymentReference(NewOrderNo(), null, null), Sandbox(), CancellationToken.None);

        Assert.False(status.Succeeded);
        Assert.False(string.IsNullOrEmpty(status.ErrorCode));
    }
}
