using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>
/// The simulated provider. Every call is a signed HTTP request to <see cref="SimulatorGateway"/> through the client's handlers, so it is
/// logged like a real provider's; notifications are verified with the same HMAC the simulator signs them with.
/// </summary>
public sealed class SimulatorPaymentProvider : IStoredCardPaymentProvider
{
    private readonly HttpClient _client;
    private readonly SimulatorBank _bank;
    private readonly SimulatorOptions _options;

    public SimulatorPaymentProvider(HttpClient client, SimulatorBank bank, SimulatorOptions options)
    {
        _client = client;
        _bank = bank;
        _options = options;
    }

    public PaymentProviderName Name => PaymentProviderName.Simulator;

    public PaymentCapabilities Capabilities =>
        PaymentCapabilities.Cancel | PaymentCapabilities.Refund | PaymentCapabilities.PartialRefund | PaymentCapabilities.StatusQuery
        | PaymentCapabilities.Recurring;

    public async Task<PaymentStartResult> StartAsync(PaymentStartRequest request, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        // The first secret a merchant uses is the one the simulated provider knows (trust on first use).
        _bank.EnsureMerchant(credentials.MerchantId, credentials.SecretKey);
        GatewayAnswer answer = await CallAsync("api/start", new GatewayStart(request.OrderNo, request.Amount.Minor, request.Amount.Currency,
            request.CallbackUrl, request.ReturnUrl, FlowOf(credentials).ToString(), request.Description, request.Buyer.FullName, request.Buyer.Email,
            request.StoreCard, request.StoredCardOwnerKey),
            credentials, cancellationToken);
        return answer.Succeeded
            ? new PaymentStartResult(true, answer.Token, $"{_options.PageBaseUrl.TrimEnd('/')}/{answer.Token}", null, null, null)
            : PaymentStartResult.Failure(answer.ErrorCode!, answer.ErrorMessage);
    }

    public PaymentMessageReference ReferenceOf(IncomingPaymentMessage message) =>
        new(message.Fields.TryGetValue("order_no", out string? orderNo) && orderNo.Length > 0 ? orderNo : null, null);

    // The simulator's messages are signed themselves: no network call.
    public Task<NotificationVerification> VerifyNotificationAsync(IncomingPaymentMessage message, MerchantCredentials credentials,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> f = message.Fields;
        bool valid = SimulatorSignature.Matches(SimulatorSignature.Compute(credentials.SecretKey, f), f.GetValueOrDefault(SimulatorSignature.HashField));
        string? Field(string name) => f.TryGetValue(name, out string? v) && v.Length > 0 ? v : null;
        Money? amount = long.TryParse(Field("total_amount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long minor)
            ? new Money(minor, Field("currency") ?? Money.DefaultCurrency)
            : null;
        PaymentResultKind? result = Field("status") switch
        {
            "success" => PaymentResultKind.Paid,
            "failed" => PaymentResultKind.Failed,
            _ => null,
        };
        return Task.FromResult(new NotificationVerification(valid, Field("order_no"), Field("merchant_id"), result, amount, Field("payment_id"),
            Field("transaction_id"), Field("failed_reason_code"), Field("card_last4"), Field("card_family"),
            int.TryParse(Field("installment_count"), CultureInfo.InvariantCulture, out int installment) ? installment : null, "OK")
        {
            StoredCard = Field("card_user_key") is string owner && Field("card_token") is string card
                ? new StoredCardDetails(new StoredCardReference(owner, card), Field("card_last4"), Field("card_family"), null)
                : null,
        });
    }

    public async Task<PaymentStatusResult> ChargeStoredCardAsync(StoredCardCharge charge, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        GatewayAnswer answer = await CallAsync("api/charge", new GatewayCharge(charge.OrderNo, charge.Amount.Minor, charge.Amount.Currency,
            charge.Card.OwnerKey, charge.Card.CardToken, charge.Description), credentials, cancellationToken);
        return answer.Succeeded
            ? new PaymentStatusResult(true, Enum.Parse<ProviderPaymentState>(answer.State!), new Money(answer.AmountMinor!.Value, answer.Currency!),
                answer.PaymentId, answer.TransactionId, answer.ErrorCode, answer.ErrorMessage)
            : PaymentStatusResult.Failure(answer.ErrorCode!, answer.ErrorMessage);
    }

    public async Task<PaymentActionResult> DeleteStoredCardAsync(StoredCardReference card, MerchantCredentials credentials, CancellationToken cancellationToken) =>
        Action(await CallAsync("api/card/delete", new GatewayCard(card.OwnerKey, card.CardToken), credentials, cancellationToken));

    public async Task<PaymentStatusResult> QueryAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        GatewayAnswer answer = await CallAsync("api/query", new GatewayReference(reference.OrderNo, null), credentials, cancellationToken);
        return answer.Succeeded
            ? new PaymentStatusResult(true, Enum.Parse<ProviderPaymentState>(answer.State!), new Money(answer.AmountMinor!.Value, answer.Currency!),
                answer.PaymentId, answer.TransactionId, null, null)
            : PaymentStatusResult.Failure(answer.ErrorCode!, answer.ErrorMessage);
    }

    public async Task<PaymentActionResult> CancelAsync(PaymentReference reference, MerchantCredentials credentials, CancellationToken cancellationToken) =>
        Action(await CallAsync("api/cancel", new GatewayReference(reference.OrderNo, null), credentials, cancellationToken));

    public async Task<PaymentActionResult> RefundAsync(RefundRequest request, MerchantCredentials credentials, CancellationToken cancellationToken) =>
        Action(await CallAsync("api/refund", new GatewayReference(request.Reference.OrderNo, request.Amount.Minor), credentials, cancellationToken));

    internal static SimulatorFlow FlowOf(MerchantCredentials credentials) =>
        credentials.Extra?.TryGetValue(SimulatorOptions.FlowKey, out string? flow) == true && Enum.TryParse(flow, out SimulatorFlow parsed)
            ? parsed
            : SimulatorFlow.HostedForm;

    private static PaymentActionResult Action(GatewayAnswer answer) =>
        answer.Succeeded ? new PaymentActionResult(true, answer.Reference, null, null) : PaymentActionResult.Failure(answer.ErrorCode!, answer.ErrorMessage);

    private async Task<GatewayAnswer> CallAsync<T>(string path, T payload, MerchantCredentials credentials, CancellationToken cancellationToken)
    {
        string body = JsonSerializer.Serialize(payload, SimulatorGateway.Json);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add(SimulatorGateway.MerchantHeader, credentials.MerchantId);
        request.Headers.Add(SimulatorGateway.KeyHeader, credentials.ApiKey);
        request.Headers.Add(SimulatorGateway.SignatureHeader, SimulatorSignature.Hmac(credentials.SecretKey, body));
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GatewayAnswer>(SimulatorGateway.Json, cancellationToken)
            ?? new GatewayAnswer(false, "empty_answer");
    }
}
