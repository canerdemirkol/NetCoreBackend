using System.Globalization;
using System.Text.Json;

namespace NetCoreBackend.NArchitecture.Core.Payments.PayTr;

/// <summary>
/// PayTR's iFrame API (dev.paytr.com). A payment gets a token and opens PayTR's page; the browser comes back with no data and the result
/// arrives as a notification signed with the merchant key and salt, answered "OK". Calls are form posts through the client's handlers (the
/// payment log). PayTR has no cancel: money goes back by refund. Provider errors are results.
/// </summary>
public sealed class PayTrPaymentProvider : IPaymentProvider, ITransactionReportProvider
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
    private const string NoTransactions = "failed";
    private const string Sale = "S";
    private const string Return = "I";
    private const string ReportDay = "yyyy-MM-dd";
    private const string DayStarts = " 00:00:00";
    private const string DayEnds = " 23:59:59";
    private const string ReportDayFormat = "dd.MM.yyyy";

    private const string InvalidAddressMessage = "The store's provider address is refused (HTTPS only).";

    private readonly HttpClient _client;
    private readonly PayTrOptions _options;

    public PayTrPaymentProvider(HttpClient client, PayTrOptions options)
    {
        _client = client;
        _options = options;
    }

    public PaymentProviderName Name => PaymentProviderName.PayTr;

    public PaymentCapabilities Capabilities =>
        PaymentCapabilities.Refund | PaymentCapabilities.PartialRefund | PaymentCapabilities.StatusQuery | PaymentCapabilities.TransactionReport;

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

        using JsonDocument? answer = await PostAsync(_options.GetTokenPath, form, credentials, cancellationToken);
        if (answer is null)
            return PaymentStartResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage);
        JsonElement a = answer.RootElement;
        return Text(a, "status") == Succeeded && Text(a, "token") is string token
            ? new PaymentStartResult(true, token, _options.PaymentPage(token), null, null, null)
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
        // E13-14b: what the customer paid above the order's amount for instalments (total_amount is the signed one).
        Money? fee = amount is Money order && long.TryParse(total, NumberStyles.Integer, CultureInfo.InvariantCulture, out long charged)
            && charged > order.Minor ? new Money(charged - order.Minor, order.Currency) : null;
        return Task.FromResult(new NotificationVerification(true, orderNo, credentials.MerchantId, result, amount, null, null,
            Field(message, "failed_reason_code"), null, null, null, NotificationAnswer) { InstallmentFee = fee });
    }

    public async Task<PaymentStatusResult> QueryAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = credentials.MerchantId,
            ["merchant_oid"] = reference.OrderNo,
            ["paytr_token"] = PayTrToken.ForStatusQuery(credentials.MerchantId, credentials.ApiKey, credentials.SecretKey, reference.OrderNo),
        };
        using JsonDocument? answer = await PostAsync(_options.StatusQueryPath, form, credentials, cancellationToken);
        if (answer is null)
            return PaymentStatusResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage);
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
        decimal? charged = Decimal(Text(a, "payment_total"));
        return new PaymentStatusResult(true, state, paid is decimal amount ? Money.Of(amount, currency) : null, null, null, null, null)
        {
            InstallmentFee = paid is decimal order && charged is decimal sum && sum > order ? Money.Of(sum - order, currency) : null,
            Installment = int.TryParse(Text(a, "taksit"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count > 1 ? count : null,
        };
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
        using JsonDocument? answer = await PostAsync(_options.RefundPath, form, credentials, cancellationToken);
        if (answer is null)
            return PaymentActionResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage);
        JsonElement a = answer.RootElement;
        return Text(a, "status") == Succeeded
            ? new PaymentActionResult(true, Text(a, "reference_no") ?? orderNo, null, null)
            : PaymentActionResult.Failure(Text(a, "err_no") ?? Text(a, "status") ?? EmptyAnswerCode, Text(a, "err_msg"));
    }

    // E13-14b: "İşlem Dökümü" for one day (dev.paytr.com/islem-dokumu; not tried against PayTR: no test store). Sales and refunds;
    // "failed" means no transactions. The docs name no list key, so the first array in the answer is read.
    public async Task<TransactionReportResult> GetTransactionsAsync(DateOnly day, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        string start = day.ToString(ReportDay, CultureInfo.InvariantCulture) + DayStarts;
        string end = day.ToString(ReportDay, CultureInfo.InvariantCulture) + DayEnds;
        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = credentials.MerchantId,
            ["start_date"] = start,
            ["end_date"] = end,
            ["paytr_token"] = PayTrToken.ForTransactionReport(credentials.MerchantId, credentials.ApiKey, credentials.SecretKey, start, end),
        };
        using JsonDocument? answer = await PostAsync(_options.TransactionReportPath, form, credentials, cancellationToken);
        if (answer is null)
            return TransactionReportResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage);
        JsonElement a = answer.RootElement;
        string? status = Text(a, "status");
        if (status == NoTransactions)
            return TransactionReportResult.Of([]);
        if (status != Succeeded)
            return TransactionReportResult.Failure(Text(a, "err_no") ?? status ?? EmptyAnswerCode, Text(a, "err_msg"));
        IEnumerable<JsonElement> rows = a.EnumerateObject().Select(p => p.Value).FirstOrDefault(v => v.ValueKind == JsonValueKind.Array) is
            { ValueKind: JsonValueKind.Array } list
            ? list.EnumerateArray()
            : Text(a, "islem_tipi") is not null ? [a] : [];
        return TransactionReportResult.Of([.. rows.Select(ReportRowOf).OfType<ProviderTransaction>()]);
    }

    private static ProviderTransaction? ReportRowOf(JsonElement row)
    {
        ProviderTransactionKind? kind = Text(row, "islem_tipi") switch
        {
            Sale => ProviderTransactionKind.Payment,
            Return => ProviderTransactionKind.Refund,
            _ => null,
        };
        if (kind is null || Decimal(Text(row, "islem_tutari")) is not decimal amount)
            return null;
        string currency = MoneyCurrency(Text(row, "para_birimi"));
        bool sale = kind == ProviderTransactionKind.Payment;
        return new ProviderTransaction(kind.Value, Text(row, "siparis_no"), null, null, Money.Of(amount, currency),
            sale && Decimal(Text(row, "odeme_tutari")) is decimal paid ? Money.Of(paid, currency) : null,
            sale && int.TryParse(Text(row, "taksit"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) && count > 1 ? count : null,
            sale && Decimal(Text(row, "kesinti_tutari")) is decimal kept ? Money.Of(kept, currency) : null,
            sale && Decimal(Text(row, "net_tutar")) is decimal net ? Money.Of(net, currency) : null,
            DateTime.TryParseExact(Text(row, "islem_tarihi"), ReportDayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime at)
                ? at
                : null);
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

    // Null when the store's own address is refused (not HTTPS): nothing is sent. Test and live share PayTR's address.
    private async Task<JsonDocument?> PostAsync(string path, Dictionary<string, string> form, MerchantCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (!ProviderBaseAddress.TryResolve(credentials, _options.BaseAddress, null, out Uri? baseAddress))
            return null;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress!, path)) { Content = new FormUrlEncodedContent(form) };
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
