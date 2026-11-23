using System.Text.Json;
using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

namespace Core.Test.Payments;

/// <summary>
/// The iyzico provider against a recorded endpoint: what each call sends (path, IYZWSv2 header, JSON body as docs.iyzico.com describes it)
/// and how each answer is read. A Checkout Form's result is believed only when the retrieved answer's signature matches.
/// </summary>
public class IyzicoPaymentProviderTests
{
    private const string OrderNo = "PO20261123A7K3Q9";
    private const string SecretKey = "sandbox-secret-key";
    private const string ApiKey = "sandbox-api-key";
    private static readonly MerchantCredentials Sandbox = new("100200", ApiKey, SecretKey, PaymentEnvironment.Test);
    private static readonly Money Amount = Money.Of(100.5m);

    private static PaymentBuyer Buyer(string? identityNumber = null, string? city = "İstanbul") =>
        new("U-1", "Ayşe", "Yılmaz", "ayse@test.local", "+905551112233", "Atatürk Cd. 1", city, "Turkey", "34000", identityNumber, "88.230.1.1");

    private static PaymentStartRequest Start(PaymentBuyer? buyer = null) => new(OrderNo, Amount, buyer ?? Buyer(),
        "https://api.test/api/payments/callback/iyzico", "https://api.test/api/payments/return/iyzico", "Pro, aylık");

    private static (IyzicoPaymentProvider Provider, RecordingHttpHandler Http) Provider(params string[] answers) => Provider(new IyzicoOptions(), answers);

    private static (IyzicoPaymentProvider Provider, RecordingHttpHandler Http) Provider(IyzicoOptions options, params string[] answers)
    {
        var http = new RecordingHttpHandler();
        foreach (string answer in answers)
            http.Answer(answer);
        return (new IyzicoPaymentProvider(new HttpClient(http), options), http);
    }

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement;

    private const string InitializeSucceeded =
        """{"status":"success","locale":"tr","systemTime":1,"conversationId":"PO20261123A7K3Q9","token":"tok-1","paymentPageUrl":"https://sandbox-cpp.iyzipay.com?token=tok-1&lang=tr"}""";

    private const string Paid =
        """{"status":"success","paymentStatus":"SUCCESS","fraudStatus":1,"paymentId":"12345678","currency":"TRY","basketId":"PO20261123A7K3Q9","conversationId":"PO20261123A7K3Q9","paidPrice":100.5,"price":100.5,"token":"tok-1","lastFourDigits":"0016","cardFamily":"Bonus","installment":1,"itemTransactions":[{"paymentTransactionId":"98765"}],"signature":"ca66b9740d106c82f3f70bdb01267bff0cfe60804bdf46f3aaeb53a55ae047af"}""";

    [Fact]
    public async Task Start_OpensACheckoutForm_SignedAndShapedAsTheDocsSay()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(InitializeSucceeded);

        PaymentStartResult started = await provider.StartAsync(Start(), Sandbox, CancellationToken.None);

        var call = http.Requests.Single();
        Assert.Equal("https://sandbox-api.iyzipay.com/payment/iyzipos/checkoutform/initialize/auth/ecom", call.Uri.ToString());
        Assert.Equal(IyzicoAuthorization.Header(ApiKey, SecretKey, call.Headers["x-iyzi-rnd"], call.Uri.AbsolutePath, call.Body), call.Headers["Authorization"]);
        JsonElement body = Json(call.Body);
        Assert.Equal(("100.5", "100.5", "TRY"), (body.GetProperty("price").GetString(), body.GetProperty("paidPrice").GetString(), body.GetProperty("currency").GetString()));
        Assert.Equal((OrderNo, OrderNo), (body.GetProperty("conversationId").GetString(), body.GetProperty("basketId").GetString()));
        Assert.Equal("https://api.test/api/payments/return/iyzico", body.GetProperty("callbackUrl").GetString());
        Assert.Equal([1], body.GetProperty("enabledInstallments").EnumerateArray().Select(i => i.GetInt32()));
        JsonElement buyer = body.GetProperty("buyer");
        Assert.Equal(("Ayşe", "Yılmaz", IyzicoPaymentProvider.PlaceholderIdentityNumber, "+905551112233", "İstanbul", "Turkey"),
            (buyer.GetProperty("name").GetString(), buyer.GetProperty("surname").GetString(), buyer.GetProperty("identityNumber").GetString(),
             buyer.GetProperty("gsmNumber").GetString(), buyer.GetProperty("city").GetString(), buyer.GetProperty("country").GetString()));
        Assert.Equal("Ayşe Yılmaz", body.GetProperty("billingAddress").GetProperty("contactName").GetString());
        JsonElement item = body.GetProperty("basketItems").EnumerateArray().Single();
        Assert.Equal((OrderNo, "Pro, aylık", "Subscription", "VIRTUAL", "100.5"),
            (item.GetProperty("id").GetString(), item.GetProperty("name").GetString(), item.GetProperty("category1").GetString(),
             item.GetProperty("itemType").GetString(), item.GetProperty("price").GetString()));
        Assert.True(started.Succeeded);
        Assert.Equal(("tok-1", "https://sandbox-cpp.iyzipay.com?token=tok-1&lang=tr"), (started.ProviderToken, started.PaymentPageUrl));
    }

    [Theory]
    [InlineData("12345678901", "12345678901")]
    [InlineData("1234567890", IyzicoPaymentProvider.PlaceholderIdentityNumber)]
    [InlineData(null, IyzicoPaymentProvider.PlaceholderIdentityNumber)]
    public async Task TheIdentityNumber_IsAnElevenDigitNumber_OrThePlaceholder(string? given, string sent)
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(InitializeSucceeded);

        await provider.StartAsync(Start(Buyer(given)), Sandbox, CancellationToken.None);

        Assert.Equal(sent, Json(http.Requests.Single().Body).GetProperty("buyer").GetProperty("identityNumber").GetString());
    }

    [Fact]
    public async Task ALiveAccount_GoesToTheLiveApi()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(InitializeSucceeded);

        await provider.StartAsync(Start(), Sandbox with { Environment = PaymentEnvironment.Live }, CancellationToken.None);

        Assert.Equal("api.iyzipay.com", http.Requests.Single().Uri.Host);
    }

    [Fact]
    public async Task AnIyzicoError_IsAResult()
    {
        (IyzicoPaymentProvider provider, _) = Provider("""{"status":"failure","errorCode":"1001","errorMessage":"api bilgileri bulunamadı","locale":"tr","systemTime":1}""");

        PaymentStartResult started = await provider.StartAsync(Start(), Sandbox, CancellationToken.None);

        Assert.False(started.Succeeded);
        Assert.Equal(("1001", "api bilgileri bulunamadı"), (started.ErrorCode, started.ErrorMessage));
    }

    [Fact]
    public async Task ABuyerWithoutACity_IsRefusedBeforeAnyCall()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider();

        PaymentStartResult started = await provider.StartAsync(Start(Buyer(city: null)), Sandbox, CancellationToken.None);

        Assert.Equal(IyzicoPaymentProvider.BuyerIncompleteCode, started.ErrorCode);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public void TheReturn_NamesItsOrderByToken_AndAWebhookByConversation()
    {
        (IyzicoPaymentProvider provider, _) = Provider();

        Assert.Equal(new PaymentMessageReference(null, "tok-1"),
            provider.ReferenceOf(new IncomingPaymentMessage(new Dictionary<string, string> { ["token"] = "tok-1" }, new Dictionary<string, string>())));
        Assert.Equal(new PaymentMessageReference(OrderNo, "tok-1"), provider.ReferenceOf(new IncomingPaymentMessage(
            new Dictionary<string, string> { ["token"] = "tok-1", ["paymentConversationId"] = OrderNo }, new Dictionary<string, string>())));
    }

    private static IncomingPaymentMessage ReturnWith(string token) =>
        new(new Dictionary<string, string> { ["token"] = token }, new Dictionary<string, string>());

    [Fact]
    public async Task TheResult_IsRetrieved_AndBelievedOnlyWithItsSignature()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(Paid);

        NotificationVerification verified = await provider.VerifyNotificationAsync(ReturnWith("tok-1"), Sandbox, CancellationToken.None);

        var call = http.Requests.Single();
        Assert.Equal("/payment/iyzipos/checkoutform/auth/ecom/detail", call.Uri.AbsolutePath);
        Assert.Equal("tok-1", Json(call.Body).GetProperty("token").GetString());
        Assert.True(verified.SignatureValid);
        Assert.Equal((OrderNo, "100200", PaymentResultKind.Paid, Amount), (verified.OrderNo, verified.MerchantId, verified.Result, verified.Amount));
        Assert.Equal(("12345678", "98765", "0016", "Bonus", 1), (verified.ProviderPaymentId, verified.ProviderTransactionId, verified.CardLast4,
            verified.CardFamily, verified.Installment));
    }

    [Fact]
    public async Task ATamperedResult_IsNotBelieved()
    {
        (IyzicoPaymentProvider provider, _) = Provider(Paid.Replace("\"paidPrice\":100.5", "\"paidPrice\":10.5"));

        NotificationVerification verified = await provider.VerifyNotificationAsync(ReturnWith("tok-1"), Sandbox, CancellationToken.None);

        Assert.False(verified.SignatureValid);
    }

    [Fact]
    public async Task AFailedPayment_IsReadAsFailed_WithItsCode()
    {
        (IyzicoPaymentProvider provider, _) = Provider(
            """{"status":"success","paymentStatus":"FAILURE","paymentId":"12345679","currency":"TRY","basketId":"PO20261123A7K3Q9","conversationId":"PO20261123A7K3Q9","paidPrice":100.5,"price":100.5,"token":"tok-2","errorCode":"10051","errorMessage":"Kart limiti yetersiz","signature":"6de8fdac22bea62cc9a59190ada2bda8b670adcb0ece7d0d0f2b8b14e88385ed"}""");

        NotificationVerification verified = await provider.VerifyNotificationAsync(ReturnWith("tok-2"), Sandbox, CancellationToken.None);

        Assert.True(verified.SignatureValid);
        Assert.Equal((PaymentResultKind.Failed, "10051"), (verified.Result, verified.ErrorCode));
    }

    // As the sandbox answers a declined card (recorded 2026-11-23): status "failure", no signature, but a payment and its status.
    private const string Declined =
        """{"status":"failure","errorCode":"10051","errorMessage":"Kart limiti yetersiz, yetersiz bakiye","errorGroup":"NOT_SUFFICIENT_FUNDS","locale":"tr","systemTime":1,"paymentId":"39083934","basketId":"PO20261123A7K3Q9","conversationId":"PO20261123A7K3Q9","mdStatus":1,"token":"tok-3","paymentStatus":"FAILURE"}""";

    private const string CallError = """{"status":"failure","errorCode":"5145","errorMessage":"Geçersiz token","locale":"tr","systemTime":1}""";

    [Fact]
    public async Task ADeclinedCard_OnTheReturn_IsAFailedPayment_NotARefusal()
    {
        (IyzicoPaymentProvider provider, _) = Provider(Declined);

        NotificationVerification verified = await provider.VerifyNotificationAsync(ReturnWith("tok-3"), Sandbox, CancellationToken.None);

        Assert.True(verified.SignatureValid);
        Assert.Equal((OrderNo, PaymentResultKind.Failed, "10051", "39083934"), (verified.OrderNo, verified.Result, verified.ErrorCode, verified.ProviderPaymentId));
        Assert.Null(verified.Amount);
    }

    [Fact]
    public async Task AnErrorWithoutAPaymentStatus_OnTheReturn_IsStillRefused()
    {
        (IyzicoPaymentProvider provider, _) = Provider(CallError);

        NotificationVerification verified = await provider.VerifyNotificationAsync(ReturnWith("tok-3"), Sandbox, CancellationToken.None);

        Assert.False(verified.SignatureValid);
        Assert.Equal("5145", verified.ErrorCode);
    }

    [Fact]
    public async Task ADeclinedCard_OnTheStatusQuery_IsAFailedPayment()
    {
        (IyzicoPaymentProvider provider, _) = Provider(Declined);

        PaymentStatusResult status = await provider.QueryAsync(new PaymentReference(OrderNo, null, null), Sandbox, CancellationToken.None);

        Assert.True(status.Succeeded);
        Assert.Equal((ProviderPaymentState.Failed, "39083934", "10051"), (status.State, status.ProviderPaymentId, status.ErrorCode));
    }

    [Fact]
    public async Task AnErrorWithoutAPaymentStatus_OnTheStatusQuery_IsStillAFailedQuery()
    {
        (IyzicoPaymentProvider provider, _) = Provider(CallError);

        PaymentStatusResult status = await provider.QueryAsync(new PaymentReference(OrderNo, null, null), Sandbox, CancellationToken.None);

        Assert.False(status.Succeeded);
        Assert.Equal("5145", status.ErrorCode);
    }

    [Fact]
    public async Task AReturnWithoutAToken_IsRefusedWithoutACall()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider();

        NotificationVerification verified = await provider.VerifyNotificationAsync(
            new IncomingPaymentMessage(new Dictionary<string, string>(), new Dictionary<string, string>()), Sandbox, CancellationToken.None);

        Assert.False(verified.SignatureValid);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task TheStatus_IsQueriedByOrder_AndItsSignatureChecked()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(
            """{"status":"success","paymentStatus":"SUCCESS","paymentId":"12345678","currency":"TRY","basketId":"PO20261123A7K3Q9","conversationId":"PO20261123A7K3Q9","paidPrice":100.5,"price":100.5,"itemTransactions":[{"paymentTransactionId":"98765"}],"signature":"fa110b1d2e7564a916ee5a845d6f7f761ebabf58d2f0842527287abb41a755ce"}""");

        PaymentStatusResult status = await provider.QueryAsync(new PaymentReference(OrderNo, null, null), Sandbox, CancellationToken.None);

        Assert.Equal("/payment/detail", http.Requests.Single().Uri.AbsolutePath);
        Assert.Equal(OrderNo, Json(http.Requests.Single().Body).GetProperty("paymentConversationId").GetString());
        Assert.True(status.Succeeded);
        Assert.Equal((ProviderPaymentState.Paid, Amount, "12345678", "98765"), (status.State, status.Amount, status.ProviderPaymentId, status.ProviderTransactionId));
    }

    [Fact]
    public async Task ACancel_NeedsThePaymentId_AndSendsIt()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider("""{"status":"success","paymentId":"12345678","price":100.5,"currency":"TRY"}""");

        PaymentActionResult withoutId = await provider.CancelAsync(new PaymentReference(OrderNo, null, null), Sandbox, CancellationToken.None);
        PaymentActionResult cancelled = await provider.CancelAsync(new PaymentReference(OrderNo, "12345678", "98765"), Sandbox, CancellationToken.None);

        Assert.Equal(IyzicoPaymentProvider.PaymentIdRequiredCode, withoutId.ErrorCode);
        Assert.Equal("/payment/cancel", http.Requests.Single().Uri.AbsolutePath);
        Assert.Equal("12345678", Json(http.Requests.Single().Body).GetProperty("paymentId").GetString());
        Assert.True(cancelled.Succeeded);
    }

    [Fact]
    public async Task ARefund_GoesByTransaction_OrByPaymentWhenTheTransactionIsUnknown()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(
            """{"status":"success","paymentId":"12345678","paymentTransactionId":"98765","price":50.25,"currency":"TRY"}""",
            """{"status":"success","paymentId":"12345678","price":50.25,"currency":"TRY"}""");

        PaymentActionResult byTransaction = await provider.RefundAsync(new RefundRequest(new PaymentReference(OrderNo, "12345678", "98765"), Money.Of(50.25m)), Sandbox,
            CancellationToken.None);
        PaymentActionResult byPayment = await provider.RefundAsync(new RefundRequest(new PaymentReference(OrderNo, "12345678", null), Money.Of(50.25m)), Sandbox,
            CancellationToken.None);

        Assert.Equal(("/payment/refund", "98765", "50.25"), (http.Requests[0].Uri.AbsolutePath,
            Json(http.Requests[0].Body).GetProperty("paymentTransactionId").GetString(), Json(http.Requests[0].Body).GetProperty("price").GetString()));
        Assert.Equal(("/v2/payment/refund", "12345678"), (http.Requests[1].Uri.AbsolutePath, Json(http.Requests[1].Body).GetProperty("paymentId").GetString()));
        Assert.True(byTransaction.Succeeded && byPayment.Succeeded);
    }

    // The application's configuration wins: a changed address or path needs no new package.
    [Fact]
    public async Task TheConfiguredPaths_AndTheAccountsOwnAddress_AreUsed_AndSigned()
    {
        var options = new IyzicoOptions { CheckoutFormInitializePath = "/v3/checkoutform/initialize" };
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(options, InitializeSucceeded);
        MerchantCredentials account = Sandbox with { Extra = new Dictionary<string, string> { [ProviderBaseAddress.Key] = "https://sandbox-v2.iyzipay.test" } };

        await provider.StartAsync(Start(), account, CancellationToken.None);

        var call = http.Requests.Single();
        Assert.Equal("https://sandbox-v2.iyzipay.test/v3/checkoutform/initialize", call.Uri.ToString());
        Assert.Equal(IyzicoAuthorization.Header(ApiKey, SecretKey, call.Headers["x-iyzi-rnd"], "/v3/checkoutform/initialize", call.Body), call.Headers["Authorization"]);
    }

    [Fact]
    public async Task ATestAccountPointedAtTheLiveApi_IsRefused_WithoutACall()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider();
        MerchantCredentials account = Sandbox with { Extra = new Dictionary<string, string> { [ProviderBaseAddress.Key] = "https://api.iyzipay.com" } };

        PaymentStartResult started = await provider.StartAsync(Start(), account, CancellationToken.None);

        Assert.Equal(ProviderBaseAddress.InvalidCode, started.ErrorCode);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public void TheCapabilities_AreCancelRefundsAndQueries()
    {
        Assert.Equal(PaymentCapabilities.Cancel | PaymentCapabilities.Refund | PaymentCapabilities.PartialRefund | PaymentCapabilities.StatusQuery,
            Provider().Provider.Capabilities);
    }
}
