using System.Text.Json;
using System.Web;
using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.PayTr;

namespace Core.Test.Payments;

/// <summary>
/// The PayTR provider against a recorded endpoint: the form fields each call sends (dev.paytr.com), the iframe page from the token, the
/// signed notification (believed only with its hash, answered "OK"), the status inquiry with refunds, and refunds. PayTR has no cancel.
/// </summary>
public class PayTrPaymentProviderTests
{
    private const string OrderNo = "PO20261123A7K3Q9";
    // MerchantId = merchant_id, ApiKey = merchant_key, SecretKey = merchant_salt.
    private static readonly MerchantCredentials TestStore = new("123456", "key-1", "salt-1", PaymentEnvironment.Test);
    private static readonly Money Amount = Money.Of(100.5m);

    private static PaymentBuyer Buyer(string? phone = "+905551112233") =>
        new("U-1", "Ayşe", "Yılmaz", "ayse@test.local", phone, "Atatürk Cd. 1", "İstanbul", "Turkey", "34000", null, "88.230.1.1");

    private static PaymentStartRequest Start(PaymentBuyer? buyer = null) => new(OrderNo, Amount, buyer ?? Buyer(),
        "https://api.test/api/payments/callback/paytr", "https://app.test/payment/result", "Pro, aylık");

    private static (PayTrPaymentProvider Provider, RecordingHttpHandler Http) Provider(params string[] answers)
    {
        var http = new RecordingHttpHandler();
        foreach (string answer in answers)
            http.Answer(answer);
        return (new PayTrPaymentProvider(new HttpClient(http)), http);
    }

    private static System.Collections.Specialized.NameValueCollection Form(string body) => HttpUtility.ParseQueryString(body);

    [Fact]
    public async Task Start_GetsAnIframeToken_WithTheDocumentedFields()
    {
        (PayTrPaymentProvider provider, RecordingHttpHandler http) = Provider("""{"status":"success","token":"abc123"}""");

        PaymentStartResult started = await provider.StartAsync(Start(), TestStore, CancellationToken.None);

        var call = http.Requests.Single();
        Assert.Equal("https://www.paytr.com/odeme/api/get-token", call.Uri.ToString());
        Assert.Equal("application/x-www-form-urlencoded", call.ContentType);
        var form = Form(call.Body);
        Assert.Equal(("123456", "88.230.1.1", OrderNo, "ayse@test.local", "10050", "TL"),
            (form["merchant_id"], form["user_ip"], form["merchant_oid"], form["email"], form["payment_amount"], form["currency"]));
        Assert.Equal(("1", "0", "1"), (form["no_installment"], form["max_installment"], form["test_mode"]));
        Assert.Equal(("Ayşe Yılmaz", "Atatürk Cd. 1, İstanbul, Turkey", "+905551112233"), (form["user_name"], form["user_address"], form["user_phone"]));
        Assert.Equal(("https://app.test/payment/result", "https://app.test/payment/result"), (form["merchant_ok_url"], form["merchant_fail_url"]));
        Assert.Equal(PayTrBasket.Encode("Pro, aylık", Amount), form["user_basket"]);
        Assert.Equal(PayTrToken.ForStart("123456", "key-1", "salt-1", "88.230.1.1", OrderNo, "ayse@test.local", "10050", form["user_basket"]!, "1", "0", "TL", "1"),
            form["paytr_token"]);
        Assert.True(started.Succeeded);
        Assert.Equal(("abc123", "https://www.paytr.com/odeme/guvenli/abc123"), (started.ProviderToken, started.PaymentPageUrl));
    }

    [Fact]
    public async Task ALiveStore_SendsNoTestMode()
    {
        (PayTrPaymentProvider provider, RecordingHttpHandler http) = Provider("""{"status":"success","token":"abc123"}""");

        await provider.StartAsync(Start(), TestStore with { Environment = PaymentEnvironment.Live }, CancellationToken.None);

        Assert.Equal("0", Form(http.Requests.Single().Body)["test_mode"]);
    }

    [Fact]
    public async Task APayTrRefusal_IsAResult()
    {
        (PayTrPaymentProvider provider, _) = Provider("""{"status":"failed","reason":"Zorunlu alan degeri gecersiz: merchant_id"}""");

        PaymentStartResult started = await provider.StartAsync(Start(), TestStore, CancellationToken.None);

        Assert.False(started.Succeeded);
        Assert.Equal("Zorunlu alan degeri gecersiz: merchant_id", started.ErrorMessage);
    }

    [Fact]
    public async Task ABuyerWithoutAPhone_IsRefusedBeforeAnyCall()
    {
        (PayTrPaymentProvider provider, RecordingHttpHandler http) = Provider();

        PaymentStartResult started = await provider.StartAsync(Start(Buyer(phone: null)), TestStore, CancellationToken.None);

        Assert.Equal(PayTrPaymentProvider.BuyerIncompleteCode, started.ErrorCode);
        Assert.Empty(http.Requests);
    }

    private static IncomingPaymentMessage Notification(string status, string total, string? hash = null, string payment = "10050") => new(
        new Dictionary<string, string>
        {
            ["merchant_oid"] = OrderNo, ["status"] = status, ["total_amount"] = total, ["payment_amount"] = payment, ["currency"] = "TL",
            ["payment_type"] = "card", ["test_mode"] = "1", ["failed_reason_code"] = status == "failed" ? "6" : "",
            ["hash"] = hash ?? PayTrToken.ForNotification("key-1", "salt-1", OrderNo, status, total),
        },
        new Dictionary<string, string>());

    [Fact]
    public async Task ASignedNotification_IsBelieved_ReadsTheOrdersAmount_AndIsAnsweredOk()
    {
        (PayTrPaymentProvider provider, RecordingHttpHandler http) = Provider();

        // An instalment payment's total includes the instalment fee; the order's own amount is payment_amount.
        NotificationVerification verified = await provider.VerifyNotificationAsync(Notification("success", "10350"), TestStore, CancellationToken.None);

        Assert.True(verified.SignatureValid);
        Assert.Equal((OrderNo, "123456", PaymentResultKind.Paid, Amount, "OK"), (verified.OrderNo, verified.MerchantId, verified.Result, verified.Amount,
            verified.ResponseToProvider));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task AFailedNotification_CarriesItsReason_AndABadHashIsRefused()
    {
        (PayTrPaymentProvider provider, _) = Provider();

        NotificationVerification failed = await provider.VerifyNotificationAsync(Notification("failed", "10050"), TestStore, CancellationToken.None);
        NotificationVerification forged = await provider.VerifyNotificationAsync(Notification("success", "10050", hash: "bm90LWEtaGFzaA=="), TestStore,
            CancellationToken.None);

        Assert.Equal((PaymentResultKind.Failed, "6"), (failed.Result, failed.ErrorCode));
        Assert.False(forged.SignatureValid);
    }

    [Fact]
    public void TheNotification_NamesItsOrder()
    {
        Assert.Equal(new PaymentMessageReference(OrderNo, null), Provider().Provider.ReferenceOf(Notification("success", "10050")));
    }

    [Fact]
    public async Task TheStatusInquiry_ReadsThePayment_AndItsRefunds()
    {
        (PayTrPaymentProvider provider, RecordingHttpHandler http) = Provider(
            """{"status":"success","payment_amount":"100.5","payment_total":"100.5","currency":"TL","test_mode":"1","returns":[{"return_amount":"40.00"}]}""",
            """{"status":"error","err_no":"004","err_msg":"merchant_oid ile basarili odeme bulunamadi"}""");

        PaymentStatusResult partlyRefunded = await provider.QueryAsync(new PaymentReference(OrderNo, null, null), TestStore, CancellationToken.None);
        PaymentStatusResult notPaid = await provider.QueryAsync(new PaymentReference(OrderNo, null, null), TestStore, CancellationToken.None);

        var form = Form(http.Requests[0].Body);
        Assert.Equal("https://www.paytr.com/odeme/durum-sorgu", http.Requests[0].Uri.ToString());
        Assert.Equal(PayTrToken.ForStatusQuery("123456", "key-1", "salt-1", OrderNo), form["paytr_token"]);
        Assert.Equal((true, ProviderPaymentState.PartiallyRefunded, Amount), (partlyRefunded.Succeeded, partlyRefunded.State, partlyRefunded.Amount));
        Assert.Equal((true, ProviderPaymentState.Pending), (notPaid.Succeeded, notPaid.State));
    }

    [Fact]
    public async Task ARefund_SendsTheAmountWithADot_AndItsToken()
    {
        (PayTrPaymentProvider provider, RecordingHttpHandler http) = Provider("""{"status":"success","is_test":1,"merchant_oid":"PO20261123A7K3Q9","return_amount":"50.25"}""");

        PaymentActionResult refunded = await provider.RefundAsync(new RefundRequest(new PaymentReference(OrderNo, null, null), Money.Of(50.25m)), TestStore,
            CancellationToken.None);

        var form = Form(http.Requests.Single().Body);
        Assert.Equal("https://www.paytr.com/odeme/iade", http.Requests.Single().Uri.ToString());
        Assert.Equal(("50.25", PayTrToken.ForRefund("123456", "key-1", "salt-1", OrderNo, "50.25")), (form["return_amount"], form["paytr_token"]));
        Assert.True(refunded.Succeeded);
    }

    [Fact]
    public async Task ARefundRefusal_IsAResult()
    {
        (PayTrPaymentProvider provider, _) = Provider("""{"status":"error","err_no":"006","err_msg":"Toplam iade tutari odeme tutarindan fazla olamaz"}""");

        PaymentActionResult refused = await provider.RefundAsync(new RefundRequest(new PaymentReference(OrderNo, null, null), Money.Of(500m)), TestStore,
            CancellationToken.None);

        Assert.Equal(("006", "Toplam iade tutari odeme tutarindan fazla olamaz"), (refused.ErrorCode, refused.ErrorMessage));
    }

    [Fact]
    public async Task PayTr_HasNoCancel()
    {
        (PayTrPaymentProvider provider, RecordingHttpHandler http) = Provider();

        PaymentActionResult cancel = await provider.CancelAsync(new PaymentReference(OrderNo, null, null), TestStore, CancellationToken.None);

        Assert.Equal(PayTrPaymentProvider.NotSupportedCode, cancel.ErrorCode);
        Assert.Empty(http.Requests);
        Assert.Equal(PaymentCapabilities.Refund | PaymentCapabilities.PartialRefund | PaymentCapabilities.StatusQuery, provider.Capabilities);
    }
}
