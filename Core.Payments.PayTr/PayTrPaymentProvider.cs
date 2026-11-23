using System.Globalization;
using System.Text.Json;

namespace NetCoreBackend.NArchitecture.Core.Payments.PayTr;

/// <summary>
/// PayTR's iFrame API (dev.paytr.com). A payment gets a token and opens PayTR's page; the browser comes back with no data and the result
/// arrives as a notification signed with the merchant key and salt, answered "OK". Calls are form posts through the client's handlers (the
/// payment log). PayTR has no cancel: money goes back by refund. Provider errors are results.
/// </summary>
public sealed class PayTrPaymentProvider : IPaymentProvider
{
    public const string BuyerIncompleteCode = "buyer_incomplete";
    public const string NotSupportedCode = "not_supported";
    public const string StartRefusedCode = "start_refused";
    public const string EmptyAnswerCode = "empty_answer";

    private const string NotificationAnswer = "OK";
    private const string Succeeded = "success";
    private const string PayTrLira = "TL";
    private const string NoSuccessfulPaymentCode = "004";
    private const string TimeoutMinutes = "30";
    private const string Language = "tr";
    private const string Yes = "1";
    private const string No = "0";
    private const string AnyInstallment = "0";

    private readonly HttpClient _client;

    public PayTrPaymentProvider(HttpClient client) => _client = client;

    public PaymentProviderName Name => PaymentProviderName.PayTr;

    public PaymentCapabilities Capabilities => PaymentCapabilities.Refund | PaymentCapabilities.PartialRefund | PaymentCapabilities.StatusQuery;

    public async Task<PaymentStartResult> StartAsync(PaymentStartRequest request, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        PaymentBuyer buyer = request.Buyer;
        string[] missing = MissingBuyerFields(buyer);
        if (missing.Length > 0)
            return PaymentStartResult.Failure(BuyerIncompleteCode, "The buyer lacks: " + string.Join(", ", missing));

        string amount = request.Amount.Minor.ToString(CultureInfo.InvariantCulture);
        string basket = PayTrBasket.Encode(request.Description, request.Amount);
        string currency = PayTrCurrency(request.Amount.Currency);
        string noInstallment = request.MaxInstallment <= 1 ? Yes : No;
        string maxInstallment = request.MaxInstallment <= 1 ? AnyInstallment : request.MaxInstallment.ToString(CultureInfo.InvariantCulture);
        bool test = credentials.Environment == PaymentEnvironment.Test;
        string testMode = test ? Yes : No;
        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = credentials.MerchantId,
            ["user_ip"] = buyer.Ip,
            ["merchant_oid"] = request.OrderNo,
            ["email"] = buyer.Email,
            ["payment_amount"] = amount,
            ["paytr_token"] = PayTrToken.ForStart(credentials.MerchantId, credentials.ApiKey, credentials.SecretKey, buyer.Ip, request.OrderNo, buyer.Email,
                amount, basket, noInstallment, maxInstallment, currency, testMode),
            ["user_basket"] = basket,
            ["debug_on"] = test ? Yes : No,
            ["no_installment"] = noInstallment,
            ["max_installment"] = maxInstallment,
            ["user_name"] = buyer.FullName,
            ["user_address"] = string.Join(", ", new[] { buyer.Address, buyer.City, buyer.Country }.Where(p => !string.IsNullOrWhiteSpace(p))),
            ["user_phone"] = buyer.Phone!,
            // PayTR only redirects the browser back, with no data: the result comes as a notification.
            ["merchant_ok_url"] = request.ReturnUrl,
            ["merchant_fail_url"] = request.ReturnUrl,
            ["timeout_limit"] = TimeoutMinutes,
            ["currency"] = currency,
            ["test_mode"] = testMode,
            ["lang"] = Language,
        };

        using JsonDocument answer = await PostAsync(PayTrEndpoints.GetToken, form, cancellationToken);
        JsonElement a = answer.RootElement;
        return Text(a, "status") == Succeeded && Text(a, "token") is string token
            ? new PaymentStartResult(true, token, PayTrEndpoints.PaymentPage(token), null, null, null)
            : PaymentStartResult.Failure(StartRefusedCode, Text(a, "reason"));
    }

    public PaymentMessageReference ReferenceOf(IncomingPaymentMessage message) => new(Field(message, "merchant_oid"), null);

    // The notification is signed itself: no network call.
    public Task<NotificationVerification> VerifyNotificationAsync(IncomingPaymentMessage message, MerchantCredentials credentials,
        CancellationToken cancellationToken)
    {
        string? orderNo = Field(message, "merchant_oid");
        string? status = Field(message, "status");
        string? total = Field(message, "total_amount");
        bool valid = orderNo is not null && status is not null && total is not null
            && PayTrToken.Matches(PayTrToken.ForNotification(credentials.ApiKey, credentials.SecretKey, orderNo, status, total), Field(message, "hash"));
        if (!valid)
            return Task.FromResult(new NotificationVerification(false, orderNo, null, null, null, null, null, null, null, null, null, null));

        PaymentResultKind? result = status switch
        {
            Succeeded => PaymentResultKind.Paid,
            "failed" => PaymentResultKind.Failed,
            _ => null,
        };
        // An instalment payment's total_amount includes the fee; the order's own amount is payment_amount.
        string? minor = Field(message, "payment_amount") ?? total;
        Money? amount = long.TryParse(minor, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? new Money(value, MoneyCurrency(Field(message, "currency")))
            : null;
        return Task.FromResult(new NotificationVerification(true, orderNo, credentials.MerchantId, result, amount, null, null,
            Field(message, "failed_reason_code"), null, null, null, NotificationAnswer));
    }

    public async Task<PaymentStatusResult> QueryAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = credentials.MerchantId,
            ["merchant_oid"] = reference.OrderNo,
            ["paytr_token"] = PayTrToken.ForStatusQuery(credentials.MerchantId, credentials.ApiKey, credentials.SecretKey, reference.OrderNo),
        };
        using JsonDocument answer = await PostAsync(PayTrEndpoints.StatusQuery, form, cancellationToken);
        JsonElement a = answer.RootElement;
        if (Text(a, "status") != Succeeded)
        {
            string code = Text(a, "err_no") ?? EmptyAnswerCode;
            // "No successful payment for this order": not paid (yet), which is an answer, not an error.
            return code == NoSuccessfulPaymentCode
                ? new PaymentStatusResult(true, ProviderPaymentState.Pending, null, null, null, code, Text(a, "err_msg"))
                : PaymentStatusResult.Failure(code, Text(a, "err_msg"));
        }

        string currency = MoneyCurrency(Text(a, "currency"));
        decimal? paid = Decimal(Text(a, "payment_amount"));
        decimal refunded = a.TryGetProperty("returns", out JsonElement returns) && returns.ValueKind == JsonValueKind.Array
            ? returns.EnumerateArray().Sum(r => Decimal(Text(r, "return_amount")) ?? 0m)
            : 0m;
        ProviderPaymentState state = refunded <= 0 ? ProviderPaymentState.Paid
            : paid is decimal total && refunded >= total ? ProviderPaymentState.Refunded
            : ProviderPaymentState.PartiallyRefunded;
        return new PaymentStatusResult(true, state, paid is decimal amount ? Money.Of(amount, currency) : null, null, null, null, null);
    }

    public Task<PaymentActionResult> CancelAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken) =>
        Task.FromResult(PaymentActionResult.Failure(NotSupportedCode, "PayTR has no cancel; refund the payment instead."));

    public async Task<PaymentActionResult> RefundAsync(RefundRequest request, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        string orderNo = request.Reference.OrderNo;
        string returnAmount = request.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = credentials.MerchantId,
            ["merchant_oid"] = orderNo,
            ["return_amount"] = returnAmount,
            ["paytr_token"] = PayTrToken.ForRefund(credentials.MerchantId, credentials.ApiKey, credentials.SecretKey, orderNo, returnAmount),
        };
        using JsonDocument answer = await PostAsync(PayTrEndpoints.Refund, form, cancellationToken);
        JsonElement a = answer.RootElement;
        return Text(a, "status") == Succeeded
            ? new PaymentActionResult(true, Text(a, "reference_no") ?? orderNo, null, null)
            : PaymentActionResult.Failure(Text(a, "err_no") ?? Text(a, "status") ?? EmptyAnswerCode, Text(a, "err_msg"));
    }

    private static string[] MissingBuyerFields(PaymentBuyer buyer) =>
    [
        .. new (string Name, string? Value)[]
        {
            ("name", buyer.FullName), ("e-mail", buyer.Email), ("phone", buyer.Phone), ("address", buyer.Address), ("ip", buyer.Ip),
        }.Where(f => string.IsNullOrWhiteSpace(f.Value)).Select(f => f.Name),
    ];

    private static string PayTrCurrency(string currency) => currency == Money.DefaultCurrency ? PayTrLira : currency;

    private static string MoneyCurrency(string? currency) => currency is null or PayTrLira ? Money.DefaultCurrency : currency;

    // The status inquiry's amounts appear with a dot or a comma ("10,8" in the docs' sample).
    private static decimal? Decimal(string? text) =>
        decimal.TryParse(text?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ? value : null;

    private async Task<JsonDocument> PostAsync(string path, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(PayTrEndpoints.BaseAddress, path)) { Content = new FormUrlEncodedContent(form) };
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        string text = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text) ? JsonDocument.Parse("{}") : JsonDocument.Parse(text);
    }

    private static string? Field(IncomingPaymentMessage message, string name) =>
        message.Fields.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;
}
