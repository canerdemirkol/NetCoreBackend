using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

/// <summary>
/// iyzico's Checkout Form (docs.iyzico.com). A payment opens iyzico's hosted page; the browser comes back with a token only, so the result is
/// retrieved by token and believed only when the answer's signature matches. Every call is an IYZWSv2-signed JSON request through the
/// client's handlers (the payment log). Provider errors are results.
/// </summary>
public sealed class IyzicoPaymentProvider : IPaymentProvider
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
        PaymentCapabilities.Cancel | PaymentCapabilities.Refund | PaymentCapabilities.PartialRefund | PaymentCapabilities.StatusQuery;

    public async Task<PaymentStartResult> StartAsync(PaymentStartRequest request, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        PaymentBuyer buyer = request.Buyer;
        string[] missing = MissingBuyerFields(buyer);
        if (missing.Length > 0)
            return PaymentStartResult.Failure(BuyerIncompleteCode, "The buyer lacks: " + string.Join(", ", missing));

        string price = IyzicoPrice.Format(request.Amount.Amount);
        var address = new JsonObject
        {
            ["contactName"] = buyer.FullName, ["city"] = buyer.City, ["country"] = buyer.Country, ["address"] = buyer.Address, ["zipCode"] = buyer.PostalCode,
        };
        var body = new JsonObject
        {
            ["locale"] = Locale,
            ["conversationId"] = request.OrderNo,
            ["price"] = price,
            ["paidPrice"] = price,
            ["currency"] = request.Amount.Currency,
            ["basketId"] = request.OrderNo,
            ["paymentGroup"] = "PRODUCT",
            // The browser returns here with the token (iyzico's "callbackUrl").
            ["callbackUrl"] = request.ReturnUrl,
            ["enabledInstallments"] = new JsonArray(Enumerable.Range(1, Math.Max(1, request.MaxInstallment)).Select(i => (JsonNode)i).ToArray()),
            ["buyer"] = new JsonObject
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
            },
            ["billingAddress"] = address,
            ["basketItems"] = new JsonArray(new JsonObject
            {
                ["id"] = request.OrderNo, ["name"] = request.Description, ["category1"] = BasketCategory, ["itemType"] = VirtualItem, ["price"] = price,
            }),
        };

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
        Money? amount = paidPrice is decimal paid ? Money.Of(paid, Text(a, "currency") ?? Money.DefaultCurrency) : null;
        return new NotificationVerification(true, Text(a, "basketId") ?? Text(a, "conversationId"), credentials.MerchantId, result, amount,
            Text(a, "paymentId"), FirstTransactionId(a), Text(a, "errorCode"), Text(a, "lastFourDigits"), Text(a, "cardFamily"), Integer(a, "installment"),
            null);
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
        string expected = IyzicoSignature.Compute(credentials.SecretKey, Text(a, "paymentId") ?? "", Text(a, "currency") ?? "", Text(a, "basketId") ?? "",
            Text(a, "conversationId") ?? "", Price(paidPrice), Price(IyzicoPrice.Read(Get(a, "price"))));
        if (!IyzicoSignature.Matches(expected, Text(a, "signature")))
            return PaymentStatusResult.Failure(SignatureMismatchCode, "The answer's signature does not match.");

        ProviderPaymentState state = Text(a, "paymentStatus") switch
        {
            PaymentSucceeded => ProviderPaymentState.Paid,
            PaymentFailed => ProviderPaymentState.Failed,
            _ => ProviderPaymentState.Pending,
        };
        Money? amount = paidPrice is decimal paid ? Money.Of(paid, Text(a, "currency") ?? Money.DefaultCurrency) : null;
        return new PaymentStatusResult(true, state, amount, Text(a, "paymentId"), FirstTransactionId(a), null, null);
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

    private static string[] MissingBuyerFields(PaymentBuyer buyer) =>
    [
        .. new (string Name, string? Value)[]
        {
            ("first name", buyer.FirstName), ("last name", buyer.LastName), ("e-mail", buyer.Email), ("phone", buyer.Phone), ("address", buyer.Address),
            ("city", buyer.City), ("country", buyer.Country),
        }.Where(f => string.IsNullOrWhiteSpace(f.Value)).Select(f => f.Name),
    ];

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
    private async Task<JsonDocument?> PostAsync(string path, JsonObject payload, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        if (!ProviderBaseAddress.TryResolve(credentials, _options.BaseAddressFor(credentials.Environment), _options.LiveBaseAddress, out Uri? baseAddress))
            return null;
        string body = payload.ToJsonString(Json);
        string randomKey = IyzicoAuthorization.NewRandomKey();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress!, path))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
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
