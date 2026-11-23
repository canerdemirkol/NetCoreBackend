using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>
/// The simulated provider's HTTP API, answered in process (no network), so a provider call is a real HTTP exchange through the client's
/// handlers (<see cref="Logging.PaymentLoggingHandler"/>). Calls are signed: the merchant id, the API key and an HMAC of the body.
/// </summary>
public sealed class SimulatorGateway : HttpMessageHandler
{
    public const string BaseAddress = "https://simulator.payments.local/";
    public const string MerchantHeader = "X-Sim-Merchant";
    public const string KeyHeader = "X-Sim-Key";
    public const string SignatureHeader = "X-Sim-Signature";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SimulatorBank _bank;

    public SimulatorGateway(SimulatorBank bank)
    {
        _bank = bank;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        string merchantId = Header(request, MerchantHeader) ?? "";
        string? secret = _bank.SecretOf(merchantId);
        if (secret is null || !SimulatorSignature.Matches(SimulatorSignature.Hmac(secret, body), Header(request, SignatureHeader)))
            return Answer(HttpStatusCode.Unauthorized, new GatewayAnswer(false, "unauthorized", "Signature does not match."));

        GatewayAnswer answer = request.RequestUri?.AbsolutePath switch
        {
            "/api/start" => Start(merchantId, JsonSerializer.Deserialize<GatewayStart>(body, Json)!),
            "/api/query" => Query(merchantId, JsonSerializer.Deserialize<GatewayReference>(body, Json)!),
            "/api/cancel" => Cancel(merchantId, JsonSerializer.Deserialize<GatewayReference>(body, Json)!),
            "/api/refund" => Refund(merchantId, JsonSerializer.Deserialize<GatewayReference>(body, Json)!),
            _ => new GatewayAnswer(false, "not_found"),
        };
        return Answer(answer.ErrorCode == "not_found" ? HttpStatusCode.NotFound : HttpStatusCode.OK, answer);
    }

    private GatewayAnswer Start(string merchantId, GatewayStart start)
    {
        if (_bank.ByOrder(merchantId, start.OrderNo) is not null)
            return new GatewayAnswer(false, "duplicate_order", "This order number was already started.");
        SimulatedPayment payment = _bank.Add(token => new SimulatedPayment
        {
            Token = token,
            OrderNo = start.OrderNo,
            MerchantId = merchantId,
            Amount = new Money(start.AmountMinor, start.Currency),
            CallbackUrl = start.CallbackUrl,
            ReturnUrl = start.ReturnUrl,
            Flow = Enum.Parse<SimulatorFlow>(start.Flow),
            Description = start.Description,
        });
        return new GatewayAnswer(true, Token: payment.Token);
    }

    private GatewayAnswer Query(string merchantId, GatewayReference reference) =>
        _bank.ByOrder(merchantId, reference.OrderNo) is SimulatedPayment p
            ? new GatewayAnswer(true, State: p.State.ToString(), AmountMinor: p.Amount.Minor, Currency: p.Amount.Currency, PaymentId: p.PaymentId,
                TransactionId: p.TransactionId)
            : new GatewayAnswer(false, "order_not_found");

    private GatewayAnswer Cancel(string merchantId, GatewayReference reference)
    {
        if (_bank.ByOrder(merchantId, reference.OrderNo) is not SimulatedPayment p)
            return new GatewayAnswer(false, "order_not_found");
        lock (_bank.Lock)
        {
            if (p.State != ProviderPaymentState.Paid)
                return new GatewayAnswer(false, "not_paid");
            if (p.DayClosed)
                return new GatewayAnswer(false, "day_closed_use_refund", "The day is closed; refund instead.");
            p.State = ProviderPaymentState.Cancelled;
            return new GatewayAnswer(true, Reference: _bank.NextId("SIM-C"));
        }
    }

    private GatewayAnswer Refund(string merchantId, GatewayReference reference)
    {
        if (_bank.ByOrder(merchantId, reference.OrderNo) is not SimulatedPayment p)
            return new GatewayAnswer(false, "order_not_found");
        lock (_bank.Lock)
        {
            if (p.State is not (ProviderPaymentState.Paid or ProviderPaymentState.PartiallyRefunded))
                return new GatewayAnswer(false, "not_paid");
            if (!p.DayClosed)
                return new GatewayAnswer(false, "day_open_use_cancel", "The day is still open; cancel instead.");
            var amount = new Money(reference.AmountMinor ?? p.Amount.Minor, p.Amount.Currency);
            if ((p.Refunded.Currency is null ? amount : p.Refunded + amount) > p.Amount)
                return new GatewayAnswer(false, "exceeds_paid_amount", "Refunds would exceed the payment.");
            p.Refunded = p.Refunded.Currency is null ? amount : p.Refunded + amount;
            p.State = p.Refunded.Minor == p.Amount.Minor ? ProviderPaymentState.Refunded : ProviderPaymentState.PartiallyRefunded;
            return new GatewayAnswer(true, Reference: _bank.NextId("SIM-R"));
        }
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private static HttpResponseMessage Answer(HttpStatusCode status, GatewayAnswer answer) =>
        new(status) { Content = JsonContent.Create(answer, options: Json) };
}
