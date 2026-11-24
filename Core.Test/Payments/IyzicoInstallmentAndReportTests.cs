using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

namespace Core.Test.Payments;

/// <summary>
/// E13-14b: an instalment payment's result (the order's amount apart from the fee the customer paid, both signed) and iyzico's daily
/// transaction report (GET, signed over the path only, paged; payments with their commission and expected payout, cancels and refunds).
/// The rows are shaped like the sandbox's (2026-11-24).
/// </summary>
public class IyzicoInstallmentAndReportTests
{
    private const string OrderNo = "PO20261124A7K3Q9";
    private const string SecretKey = "sandbox-secret-key";
    private const string ApiKey = "sandbox-api-key";
    private static readonly MerchantCredentials Sandbox = new("100200", ApiKey, SecretKey, PaymentEnvironment.Test);

    private static (IyzicoPaymentProvider Provider, RecordingHttpHandler Http) Provider(params string[] answers)
    {
        var http = new RecordingHttpHandler();
        foreach (string answer in answers)
            http.Answer(answer);
        return (new IyzicoPaymentProvider(new HttpClient(http), new IyzicoOptions()), http);
    }

    [Fact]
    public async Task AnInstalmentResult_IsTheOrdersAmount_AndTheFeeApart()
    {
        string signature = IyzicoSignature.Compute(SecretKey, "SUCCESS", "12345678", "TRY", OrderNo, OrderNo, "330", "300", "tok-1");
        (IyzicoPaymentProvider provider, _) = Provider(
            $$"""{"status":"success","paymentStatus":"SUCCESS","fraudStatus":1,"paymentId":"12345678","currency":"TRY","basketId":"{{OrderNo}}","conversationId":"{{OrderNo}}","paidPrice":330,"price":300,"installment":3,"token":"tok-1","signature":"{{signature}}"}""");

        NotificationVerification verified = await provider.VerifyNotificationAsync(
            new IncomingPaymentMessage(new Dictionary<string, string> { ["token"] = "tok-1" }, new Dictionary<string, string>()), Sandbox, CancellationToken.None);

        Assert.True(verified.SignatureValid);
        Assert.Equal((Money.Of(300m), Money.Of(30m), 3), (verified.Amount, verified.InstallmentFee, verified.Installment));
    }

    [Fact]
    public async Task ASinglePaymentResult_HasNoFee()
    {
        string signature = IyzicoSignature.Compute(SecretKey, "SUCCESS", "12345678", "TRY", OrderNo, OrderNo, "300", "300", "tok-1");
        (IyzicoPaymentProvider provider, _) = Provider(
            $$"""{"status":"success","paymentStatus":"SUCCESS","paymentId":"12345678","currency":"TRY","basketId":"{{OrderNo}}","conversationId":"{{OrderNo}}","paidPrice":300,"price":300,"installment":1,"token":"tok-1","signature":"{{signature}}"}""");

        NotificationVerification verified = await provider.VerifyNotificationAsync(
            new IncomingPaymentMessage(new Dictionary<string, string> { ["token"] = "tok-1" }, new Dictionary<string, string>()), Sandbox, CancellationToken.None);

        Assert.Null(verified.InstallmentFee);
    }

    [Fact]
    public async Task TheStatusQuery_AlsoGivesTheOrdersAmount_AndTheFee()
    {
        string signature = IyzicoSignature.Compute(SecretKey, "12345678", "TRY", OrderNo, OrderNo, "330", "300");
        (IyzicoPaymentProvider provider, _) = Provider(
            $$"""{"status":"success","paymentStatus":"SUCCESS","paymentId":"12345678","currency":"TRY","basketId":"{{OrderNo}}","conversationId":"{{OrderNo}}","paidPrice":330,"price":300,"installment":3,"signature":"{{signature}}"}""");

        PaymentStatusResult status = await provider.QueryAsync(new PaymentReference(OrderNo, null, null), Sandbox, CancellationToken.None);

        Assert.Equal((ProviderPaymentState.Paid, Money.Of(300m), Money.Of(30m), 3), (status.State, status.Amount, status.InstallmentFee, status.Installment));
    }

    private const string PageOne =
        """{"status":"success","currentPage":1,"totalPageCount":2,"transactions":[{"transactionType":"PAYMENT","transactionStatus":2,"transactionDate":"2026-11-23 16:47:05","basketId":"PO20261123C74N76","paymentId":39084003,"paymentTxId":41056923,"price":300.0,"paidPrice":330.0,"installment":3,"iyzicoCommission":3.3,"iyzicoFee":0.25,"merchantPayoutAmount":296.45,"transactionCurrency":"TRY"},{"transactionType":"REFUND","transactionStatus":null,"transactionDate":"2026-11-23 16:48:29","basketId":"PO20261123C74N76","paymentId":39084003,"paymentTxId":41056923,"price":100.0,"paidPrice":330.0,"installment":3,"iyzicoCommission":null,"iyzicoFee":null,"merchantPayoutAmount":null,"transactionCurrency":"TRY"}]}""";

    private const string PageTwo =
        """{"status":"success","currentPage":2,"totalPageCount":2,"transactions":[{"transactionType":"CANCEL","transactionStatus":null,"transactionDate":"2026-11-23 15:05:48","basketId":"PO20261123ATHTG4","paymentId":39081549,"paymentTxId":41054530,"price":1.0,"paidPrice":1.0,"installment":1,"transactionCurrency":"TRY"},{"transactionType":"PAYMENT","transactionStatus":0,"transactionDate":"2026-11-23 15:00:00","basketId":"PO20261123FAIL01","paymentId":39081500,"paymentTxId":41054500,"price":1.0,"paidPrice":1.0,"installment":1,"transactionCurrency":"TRY"}]}""";

    [Fact]
    public async Task TheDailyReport_IsReadPageByPage_SignedOverThePathOnly()
    {
        (IyzicoPaymentProvider provider, RecordingHttpHandler http) = Provider(PageOne, PageTwo);

        TransactionReportResult report = await provider.GetTransactionsAsync(new DateOnly(2026, 11, 23), Sandbox, CancellationToken.None);

        Assert.Equal(2, http.Requests.Count);
        Assert.All(http.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.Equal("/v2/reporting/payment/transactions?transactionDate=2026-11-23&page=1", http.Requests[0].Uri.PathAndQuery);
        Assert.Equal("/v2/reporting/payment/transactions?transactionDate=2026-11-23&page=2", http.Requests[1].Uri.PathAndQuery);
        var first = http.Requests[0];
        Assert.Equal(IyzicoAuthorization.Header(ApiKey, SecretKey, first.Headers["x-iyzi-rnd"], "/v2/reporting/payment/transactions", ""),
            first.Headers["Authorization"]);
        Assert.True(report.Succeeded);
        Assert.True(provider.Capabilities.HasFlag(PaymentCapabilities.TransactionReport));
    }

    [Fact]
    public async Task TheDailyReport_HasPaymentsWithTheirCommission_CancelsAndRefunds_AndSkipsFailedPayments()
    {
        (IyzicoPaymentProvider provider, _) = Provider(PageOne, PageTwo);

        TransactionReportResult report = await provider.GetTransactionsAsync(new DateOnly(2026, 11, 23), Sandbox, CancellationToken.None);

        Assert.Equal(3, report.Transactions.Count);
        ProviderTransaction payment = report.Transactions[0];
        Assert.Equal((ProviderTransactionKind.Payment, "PO20261123C74N76", "39084003", "41056923"),
            (payment.Kind, payment.OrderNo, payment.ProviderPaymentId, payment.ProviderTransactionId));
        Assert.Equal((Money.Of(300m), Money.Of(330m), 3, Money.Of(3.55m), Money.Of(296.45m)),
            (payment.Amount, payment.Charged, payment.Installment, payment.Commission, payment.Payout));
        Assert.Equal(new DateTime(2026, 11, 23, 16, 47, 5), payment.ProviderLocalTime);
        Assert.Equal((ProviderTransactionKind.Refund, Money.Of(100m)), (report.Transactions[1].Kind, report.Transactions[1].Amount));
        Assert.Equal((ProviderTransactionKind.Cancel, "PO20261123ATHTG4", Money.Of(1m)),
            (report.Transactions[2].Kind, report.Transactions[2].OrderNo, report.Transactions[2].Amount));
    }

    [Fact]
    public async Task AReportError_IsAResult()
    {
        (IyzicoPaymentProvider provider, _) = Provider("""{"status":"failure","errorCode":"8","errorMessage":"Authentication token is not verified"}""");

        TransactionReportResult report = await provider.GetTransactionsAsync(new DateOnly(2026, 11, 23), Sandbox, CancellationToken.None);

        Assert.False(report.Succeeded);
        Assert.Equal("8", report.ErrorCode);
    }
}
