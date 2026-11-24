using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

/// <summary>
/// iyzico's Checkout Form (docs.iyzico.com). A payment opens iyzico's hosted page; the browser comes back with a token only, so the result is
/// retrieved by token and believed only when the answer's signature matches. Every call is an IYZWSv2-signed JSON request through the
/// client's handlers (the payment log). Provider errors are results. A renewal charges the card iyzico stored at the first payment
/// (/payment/auth, no 3D) and is believed only with its signature.
/// </summary>
public sealed class IyzicoPaymentProvider : IStoredCardPaymentProvider, ITransactionReportProvider
{
    /// <summary>iyzico requires an identity number; a company (10-digit tax number) or an unknown one is sent as this.</summary>
    public const string PlaceholderIdentityNumber = "11111111111";
    public const string BuyerIncompleteCode = "buyer_incomplete";
    public const string PaymentIdRequiredCode = "payment_id_required";
    public const string SignatureMismatchCode = "signature_mismatch";
    public const string EmptyAnswerCode = "empty_answer";
    private const string InvalidAddressMessage = "The merchant account's provider address is refused (HTTPS only; never the live API on a test account).";

    private const int IdentityNumberLength = 11;
    private const string Locale = "tr";
    private const string BasketCategory = "Subscription";
    private const string VirtualItem = "VIRTUAL";
    private const string ProductGroup = "PRODUCT";
    private const string SubscriptionGroup = "SUBSCRIPTION";
    private const string WebChannel = "WEB";
    private const int SingleInstallment = 1;
    private const int PaidTransaction = 2;
    private const string ReportDateFormat = "yyyy-MM-dd";
    private const string ReportTimeFormat = "yyyy-MM-dd HH:mm:ss";
    private const string Succeeded = "success";
    private const string PaymentSucceeded = "SUCCESS";
    private const string PaymentFailed = "FAILURE";
    private const int FraudApproved = 1;
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly HttpClient _client;
    private readonly IyzicoOptions _options;

    public IyzicoPaymentProvider(HttpClient client, IyzicoOptions options)
    {
        _client = client;
        _options = options;
    }

    public PaymentProviderName Name => PaymentProviderName.Iyzico;

    public PaymentCapabilities Capabilities =>
        PaymentCapabilities.Cancel | PaymentCapabilities.Refund | PaymentCapabilities.PartialRefund | PaymentCapabilities.StatusQuery
        | PaymentCapabilities.Recurring | PaymentCapabilities.TransactionReport;

    public async Task<PaymentStartResult> StartAsync(PaymentStartRequest request, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        PaymentBuyer buyer = request.Buyer;
        string[] missing = MissingBuyerFields(buyer);
        if (missing.Length > 0)
            return PaymentStartResult.Failure(BuyerIncompleteCode, "The buyer lacks: " + string.Join(", ", missing));

        string price = IyzicoPrice.Format(request.Amount.Amount);
        var body = new JsonObject
        {
            ["locale"] = Locale,
            ["conversationId"] = request.OrderNo,
            ["price"] = price,
            ["paidPrice"] = price,
            ["currency"] = request.Amount.Currency,
            ["basketId"] = request.OrderNo,
            ["paymentGroup"] = ProductGroup,
            // The browser returns here with the token (iyzico's "callbackUrl").
            ["callbackUrl"] = request.ReturnUrl,
            ["enabledInstallments"] = new JsonArray(Enumerable.Range(1, Math.Max(1, request.MaxInstallment)).Select(i => (JsonNode)i).ToArray()),
            ["buyer"] = BuyerOf(buyer),
            ["billingAddress"] = AddressOf(buyer),
            ["basketItems"] = BasketOf(request.OrderNo, request.Description, price),
        };
        // The customer's earlier cards stay under one key at iyzico.
        if (request.StoredCardOwnerKey is not null)
            body["cardUserKey"] = request.StoredCardOwnerKey;

        using JsonDocument? answer = await PostAsync(_options.CheckoutFormInitializePath, body, credentials, cancellationToken);
        if (answer is null)
            return PaymentStartResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage);
        JsonElement a = answer.RootElement;
        return Text(a, "status") == Succeeded
            ? new PaymentStartResult(true, Text(a, "token"), Text(a, "paymentPageUrl"), null, null, null)
            : PaymentStartResult.Failure(Text(a, "errorCode") ?? EmptyAnswerCode, Text(a, "errorMessage"));
    }

    // The browser's return carries the token; a webhook adds the order number as paymentConversationId.
    public PaymentMessageReference ReferenceOf(IncomingPaymentMessage message) =>
        new(Field(message, "paymentConversationId"), Field(message, "token"));

    public async Task<NotificationVerification> VerifyNotificationAsync(IncomingPaymentMessage message, MerchantCredentials credentials,
        CancellationToken cancellationToken)
    {
        string? token = Field(message, "token");
        if (token is null || !WebhookSignatureHolds(message, credentials.SecretKey))
            return Refused(null);

        var body = new JsonObject { ["locale"] = Locale, ["token"] = token };
        using JsonDocument? answer = await PostAsync(_options.CheckoutFormResultPath, body, credentials, cancellationToken);
        if (answer is null)
            return Refused(ProviderBaseAddress.InvalidCode);
        JsonElement a = answer.RootElement;
        if (IsDeclined(a))
            return new NotificationVerification(true, Text(a, "basketId") ?? Text(a, "conversationId"), credentials.MerchantId, PaymentResultKind.Failed,
                null, Text(a, "paymentId"), null, Text(a, "errorCode"), null, null, null, null);
        if (Text(a, "status") != Succeeded)
            return Refused(Text(a, "errorCode") ?? EmptyAnswerCode);

        decimal? paidPrice = IyzicoPrice.Read(Get(a, "paidPrice"));
        decimal? listPrice = IyzicoPrice.Read(Get(a, "price"));
        string expected = IyzicoSignature.Compute(credentials.SecretKey, Text(a, "paymentStatus") ?? "", Text(a, "paymentId") ?? "", Text(a, "currency") ?? "",
            Text(a, "basketId") ?? "", Text(a, "conversationId") ?? "", Price(paidPrice), Price(listPrice), Text(a, "token") ?? "");
        if (!IyzicoSignature.Matches(expected, Text(a, "signature")))
            return Refused(SignatureMismatchCode);

        PaymentResultKind? result = Text(a, "paymentStatus") switch
        {
            // A payment held for a fraud review is neither paid nor failed yet; the status query settles it later.
            PaymentSucceeded => Integer(a, "fraudStatus") is null or FraudApproved ? PaymentResultKind.Paid : null,
            PaymentFailed => PaymentResultKind.Failed,
            _ => null,
        };
        string currency = Text(a, "currency") ?? Money.DefaultCurrency;
        return new NotificationVerification(true, Text(a, "basketId") ?? Text(a, "conversationId"), credentials.MerchantId, result,
            OrderAmountOf(listPrice, paidPrice, currency), Text(a, "paymentId"), FirstTransactionId(a), Text(a, "errorCode"), Text(a, "lastFourDigits"),
            Text(a, "cardFamily"), Integer(a, "installment"), null)
        {
            StoredCard = StoredCardOf(a),
            InstallmentFee = FeeOf(listPrice, paidPrice, currency),
        };
    }

    public async Task<PaymentStatusResult> ChargeStoredCardAsync(StoredCardCharge charge, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        string[] missing = MissingBuyerFields(charge.Buyer);
        if (missing.Length > 0)
            return PaymentStatusResult.Failure(BuyerIncompleteCode, "The buyer lacks: " + string.Join(", ", missing));

        string price = IyzicoPrice.Format(charge.Amount.Amount);
        var body = new JsonObject
        {
            ["locale"] = Locale,
            ["conversationId"] = charge.OrderNo,
            ["price"] = price,
            ["paidPrice"] = price,
            ["currency"] = charge.Amount.Currency,
            ["installment"] = SingleInstallment,
            ["basketId"] = charge.OrderNo,
            ["paymentChannel"] = WebChannel,
            ["paymentGroup"] = SubscriptionGroup,
            ["paymentCard"] = new JsonObject { ["cardUserKey"] = charge.Card.OwnerKey, ["cardToken"] = charge.Card.CardToken },
            ["buyer"] = BuyerOf(charge.Buyer),
            ["billingAddress"] = AddressOf(charge.Buyer),
            ["basketItems"] = BasketOf(charge.OrderNo, charge.Description, price),
        };

        using JsonDocument? answer = await PostAsync(_options.StoredCardChargePath, body, credentials, cancellationToken);
        if (answer is null)
            return PaymentStatusResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage);
        JsonElement a = answer.RootElement;
        // A declined card: status "failure" with the payment's id and no signature (the sandbox, 2026-11-23). Without a payment id the
        // result is unknown and the caller asks again.
        if (Text(a, "status") != Succeeded)
            return Text(a, "paymentId") is string declinedId
                ? new PaymentStatusResult(true, ProviderPaymentState.Failed, null, declinedId, null, Text(a, "errorCode"), Text(a, "errorMessage"))
                : PaymentStatusResult.Failure(Text(a, "errorCode") ?? EmptyAnswerCode, Text(a, "errorMessage"));

        decimal? paidPrice = IyzicoPrice.Read(Get(a, "paidPrice"));
        string expected = IyzicoSignature.Compute(credentials.SecretKey, Text(a, "paymentId") ?? "", Text(a, "currency") ?? "", Text(a, "basketId") ?? "",
            Text(a, "conversationId") ?? "", Price(paidPrice), Price(IyzicoPrice.Read(Get(a, "price"))));
        if (!IyzicoSignature.Matches(expected, Text(a, "signature")))
            return PaymentStatusResult.Failure(SignatureMismatchCode, "The answer's signature does not match.");

        // A payment held for a fraud review is not paid yet; the status query settles it later.
        ProviderPaymentState state = Integer(a, "fraudStatus") is null or FraudApproved ? ProviderPaymentState.Paid : ProviderPaymentState.Pending;
        string currency = Text(a, "currency") ?? Money.DefaultCurrency;
        return new PaymentStatusResult(true, state, OrderAmountOf(IyzicoPrice.Read(Get(a, "price")), paidPrice, currency), Text(a, "paymentId"),
            FirstTransactionId(a), null, null);
    }

    public async Task<PaymentActionResult> DeleteStoredCardAsync(StoredCardReference card, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["locale"] = Locale, ["cardUserKey"] = card.OwnerKey, ["cardToken"] = card.CardToken };
        using JsonDocument? answer = await SendAsync(HttpMethod.Delete, _options.StoredCardPath, body, credentials, cancellationToken);
        return answer is null
            ? PaymentActionResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage)
            : Action(answer.RootElement, null);
    }

    public async Task<PaymentStatusResult> QueryAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["locale"] = Locale, ["conversationId"] = reference.OrderNo };
        if (reference.ProviderPaymentId is not null)
            body["paymentId"] = reference.ProviderPaymentId;
        else
            body["paymentConversationId"] = reference.OrderNo;

        using JsonDocument? answer = await PostAsync(_options.PaymentDetailPath, body, credentials, cancellationToken);
        if (answer is null)
            return PaymentStatusResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage);
        JsonElement a = answer.RootElement;
        if (IsDeclined(a))
            return new PaymentStatusResult(true, ProviderPaymentState.Failed, null, Text(a, "paymentId"), null, Text(a, "errorCode"), Text(a, "errorMessage"));
        if (Text(a, "status") != Succeeded)
            return PaymentStatusResult.Failure(Text(a, "errorCode") ?? EmptyAnswerCode, Text(a, "errorMessage"));

        decimal? paidPrice = IyzicoPrice.Read(Get(a, "paidPrice"));
        decimal? listPrice = IyzicoPrice.Read(Get(a, "price"));
        string expected = IyzicoSignature.Compute(credentials.SecretKey, Text(a, "paymentId") ?? "", Text(a, "currency") ?? "", Text(a, "basketId") ?? "",
            Text(a, "conversationId") ?? "", Price(paidPrice), Price(listPrice));
        if (!IyzicoSignature.Matches(expected, Text(a, "signature")))
            return PaymentStatusResult.Failure(SignatureMismatchCode, "The answer's signature does not match.");

        ProviderPaymentState state = Text(a, "paymentStatus") switch
        {
            PaymentSucceeded => ProviderPaymentState.Paid,
            PaymentFailed => ProviderPaymentState.Failed,
            _ => ProviderPaymentState.Pending,
        };
        string currency = Text(a, "currency") ?? Money.DefaultCurrency;
        return new PaymentStatusResult(true, state, OrderAmountOf(listPrice, paidPrice, currency), Text(a, "paymentId"), FirstTransactionId(a), null, null)
        {
            InstallmentFee = FeeOf(listPrice, paidPrice, currency),
            Installment = Integer(a, "installment"),
        };
    }

    // A same-day void of the whole payment.
    public async Task<PaymentActionResult> CancelAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        if (reference.ProviderPaymentId is null)
            return PaymentActionResult.Failure(PaymentIdRequiredCode, "A cancel needs iyzico's payment id.");
        var body = new JsonObject { ["locale"] = Locale, ["conversationId"] = reference.OrderNo, ["paymentId"] = reference.ProviderPaymentId };
        using JsonDocument? answer = await PostAsync(_options.CancelPath, body, credentials, cancellationToken);
        return answer is null
            ? PaymentActionResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage)
            : Action(answer.RootElement, reference.ProviderPaymentId);
    }

    // By item transaction (iyzico's recommendation for baskets); by payment when the transaction id is unknown.
    public async Task<PaymentActionResult> RefundAsync(RefundRequest request, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        PaymentReference reference = request.Reference;
        var body = new JsonObject
        {
            ["locale"] = Locale, ["conversationId"] = reference.OrderNo, ["price"] = IyzicoPrice.Format(request.Amount.Amount), ["currency"] = request.Amount.Currency,
        };
        string path;
        if (reference.ProviderTransactionId is not null)
        {
            body["paymentTransactionId"] = reference.ProviderTransactionId;
            path = _options.RefundByTransactionPath;
        }
        else if (reference.ProviderPaymentId is not null)
        {
            body["paymentId"] = reference.ProviderPaymentId;
            path = _options.RefundByPaymentPath;
        }
        else
        {
            return PaymentActionResult.Failure(PaymentIdRequiredCode, "A refund needs iyzico's payment or transaction id.");
        }

        using JsonDocument? answer = await PostAsync(path, body, credentials, cancellationToken);
        return answer is null
            ? PaymentActionResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage)
            : Action(answer.RootElement, reference.ProviderTransactionId ?? reference.ProviderPaymentId);
    }

    // The documented daily report (docs.iyzico.com: Reporting, transactions): GET, page by page; IYZWSv2 over the path alone (the sandbox
    // refuses a signature that includes the query string, 2026-11-24). A payment that did not go through (status other than 2) is left out.
    public async Task<TransactionReportResult> GetTransactionsAsync(DateOnly day, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        var transactions = new List<ProviderTransaction>();
        for (int page = 1; ; page++)
        {
            string query = string.Create(CultureInfo.InvariantCulture, $"?transactionDate={day.ToString(ReportDateFormat, CultureInfo.InvariantCulture)}&page={page}");
            using JsonDocument? answer = await SendAsync(HttpMethod.Get, _options.TransactionReportPath, null, credentials, cancellationToken, query);
            if (answer is null)
                return TransactionReportResult.Failure(ProviderBaseAddress.InvalidCode, InvalidAddressMessage);
            JsonElement a = answer.RootElement;
            if (Text(a, "status") != Succeeded)
                return TransactionReportResult.Failure(Text(a, "errorCode") ?? EmptyAnswerCode, Text(a, "errorMessage"));
            if (Get(a, "transactions") is { ValueKind: JsonValueKind.Array } rows)
                transactions.AddRange(rows.EnumerateArray().Select(ReportRowOf).OfType<ProviderTransaction>());
            if (page >= (Integer(a, "totalPageCount") ?? page))
                return TransactionReportResult.Of(transactions);
        }
    }

    private static ProviderTransaction? ReportRowOf(JsonElement row)
    {
        ProviderTransactionKind? kind = Text(row, "transactionType") switch
        {
            "PAYMENT" => ProviderTransactionKind.Payment,
            "CANCEL" => ProviderTransactionKind.Cancel,
            "REFUND" => ProviderTransactionKind.Refund,
            _ => null,
        };
        if (kind is null || (kind == ProviderTransactionKind.Payment && Integer(row, "transactionStatus") is int status && status != PaidTransaction)
            || IyzicoPrice.Read(Get(row, "price")) is not decimal price)
            return null;
        string currency = Text(row, "transactionCurrency") ?? Money.DefaultCurrency;
        decimal? rate = IyzicoPrice.Read(Get(row, "iyzicoCommission"));
        decimal? fee = IyzicoPrice.Read(Get(row, "iyzicoFee"));
        decimal? commission = rate is null && fee is null ? null : (rate ?? 0m) + (fee ?? 0m);
        bool payment = kind == ProviderTransactionKind.Payment;
        return new ProviderTransaction(kind.Value, Text(row, "basketId") ?? Text(row, "conversationId"), Text(row, "paymentId"), Text(row, "paymentTxId"),
            Money.Of(price, currency), payment && IyzicoPrice.Read(Get(row, "paidPrice")) is decimal paid ? Money.Of(paid, currency) : null,
            payment ? Integer(row, "installment") : null, payment && commission is decimal kept ? Money.Of(kept, currency) : null,
            payment && IyzicoPrice.Read(Get(row, "merchantPayoutAmount")) is decimal payout ? Money.Of(payout, currency) : null,
            DateTime.TryParseExact(Text(row, "transactionDate"), ReportTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime at)
                ? at
                : null);
    }

    // E13-14b: the order's own amount (the basket price), and what the customer paid above it for instalments.
    private static Money? OrderAmountOf(decimal? listPrice, decimal? paidPrice, string currency) =>
        (listPrice ?? paidPrice) is decimal amount ? Money.Of(amount, currency) : null;

    private static Money? FeeOf(decimal? listPrice, decimal? paidPrice, string currency) =>
        listPrice is decimal list && paidPrice is decimal paid && paid > list ? Money.Of(paid - list, currency) : null;

    private static string[] MissingBuyerFields(PaymentBuyer buyer) =>
    [
        .. new (string Name, string? Value)[]
        {
            ("first name", buyer.FirstName), ("last name", buyer.LastName), ("e-mail", buyer.Email), ("phone", buyer.Phone), ("address", buyer.Address),
            ("city", buyer.City), ("country", buyer.Country),
        }.Where(f => string.IsNullOrWhiteSpace(f.Value)).Select(f => f.Name),
    ];

    private static JsonObject BuyerOf(PaymentBuyer buyer) => new()
    {
        ["id"] = buyer.Id,
        ["name"] = buyer.FirstName,
        ["surname"] = buyer.LastName,
        ["identityNumber"] = IdentityNumberOf(buyer),
        ["email"] = buyer.Email,
        ["gsmNumber"] = buyer.Phone,
        ["registrationAddress"] = buyer.Address,
        ["city"] = buyer.City,
        ["country"] = buyer.Country,
        ["zipCode"] = buyer.PostalCode,
        ["ip"] = buyer.Ip,
    };

    private static JsonObject AddressOf(PaymentBuyer buyer) => new()
    {
        ["contactName"] = buyer.FullName, ["city"] = buyer.City, ["country"] = buyer.Country, ["address"] = buyer.Address, ["zipCode"] = buyer.PostalCode,
    };

    private static JsonArray BasketOf(string orderNo, string description, string price) => new(new JsonObject
    {
        ["id"] = orderNo, ["name"] = description, ["category1"] = BasketCategory, ["itemType"] = VirtualItem, ["price"] = price,
    });

    // The Checkout Form names the card it stored (the merchant's card storage is on and the customer kept the card).
    private static StoredCardDetails? StoredCardOf(JsonElement answer) =>
        Text(answer, "cardUserKey") is string owner && Text(answer, "cardToken") is string token
            ? new StoredCardDetails(new StoredCardReference(owner, token), Text(answer, "lastFourDigits"), Text(answer, "cardFamily"),
                Text(answer, "cardAssociation"))
            : null;

    private static string IdentityNumberOf(PaymentBuyer buyer) =>
        buyer.IdentityNumber is { Length: IdentityNumberLength } number && number.All(char.IsAsciiDigit) ? number : PlaceholderIdentityNumber;

    private static bool WebhookSignatureHolds(IncomingPaymentMessage message, string secretKey)
    {
        string? signature = message.Headers.FirstOrDefault(h => string.Equals(h.Key, IyzicoSignature.WebhookHeader, StringComparison.OrdinalIgnoreCase)).Value;
        if (signature is null)
            return true;
        string expected = IyzicoSignature.Webhook(secretKey, Field(message, "iyziEventType") ?? "", Field(message, "iyziPaymentId") ?? "",
            Field(message, "token") ?? "", Field(message, "paymentConversationId") ?? "", Field(message, "status") ?? "");
        return IyzicoSignature.Matches(expected, signature);
    }

    // A declined card answers status "failure" with the payment and its status, and without a signature. It is believed unsigned: we asked
    // iyzico ourselves over HTTPS, and a failure gives nothing away (a paid result still needs its signature). An error without a payment
    // status (a bad token, wrong keys) stays an error.
    private static bool IsDeclined(JsonElement answer) =>
        Text(answer, "status") != Succeeded && Text(answer, "paymentStatus") == PaymentFailed && Text(answer, "paymentId") is not null;

    private static NotificationVerification Refused(string? errorCode) =>
        new(false, null, null, null, null, null, null, errorCode, null, null, null, null);

    private static PaymentActionResult Action(JsonElement answer, string? reference) =>
        Text(answer, "status") == Succeeded
            ? new PaymentActionResult(true, Text(answer, "refundHostReference") ?? Text(answer, "paymentTransactionId") ?? reference, null, null)
            : PaymentActionResult.Failure(Text(answer, "errorCode") ?? EmptyAnswerCode, Text(answer, "errorMessage"));

    // Null when the account's own address is refused (not HTTPS, or the live API on a test account): nothing is sent.
    private Task<JsonDocument?> PostAsync(string path, JsonObject payload, MerchantCredentials credentials, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, payload, credentials, cancellationToken);

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path, JsonObject? payload, MerchantCredentials credentials,
        CancellationToken cancellationToken, string query = "")
    {
        if (!ProviderBaseAddress.TryResolve(credentials, _options.BaseAddressFor(credentials.Environment), _options.LiveBaseAddress, out Uri? baseAddress))
            return null;
        string body = payload?.ToJsonString(Json) ?? "";
        string randomKey = IyzicoAuthorization.NewRandomKey();
        using var request = new HttpRequestMessage(method, new Uri(baseAddress!, path + query));
        if (payload is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Add(IyzicoAuthorization.RandomKeyHeader, randomKey);
        request.Headers.TryAddWithoutValidation("Authorization", IyzicoAuthorization.Header(credentials.ApiKey, credentials.SecretKey, randomKey, path, body));
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        string text = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text) ? JsonDocument.Parse("{}") : JsonDocument.Parse(text);
    }

    private static string? Field(IncomingPaymentMessage message, string name) =>
        message.Fields.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;

    private static JsonElement Get(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) ? value : default;

    private static string? Text(JsonElement element, string name)
    {
        JsonElement value = Get(element, name);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static int? Integer(JsonElement element, string name) =>
        Get(element, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int number) ? number : null;

    private static string Price(decimal? value) => value is decimal amount ? IyzicoPrice.Format(amount) : "";

    private static string? FirstTransactionId(JsonElement answer) =>
        Get(answer, "itemTransactions") is { ValueKind: JsonValueKind.Array } items && items.GetArrayLength() > 0
            ? Text(items[0], "paymentTransactionId")
            : null;
}
