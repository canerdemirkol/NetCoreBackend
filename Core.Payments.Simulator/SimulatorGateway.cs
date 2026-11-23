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
            "/api/charge" => Charge(merchantId, JsonSerializer.Deserialize<GatewayCharge>(body, Json)!),
            "/api/card/delete" => DeleteCard(merchantId, JsonSerializer.Deserialize<GatewayCard>(body, Json)!),
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
            StoreCard = start.StoreCard,
            OwnerKey = start.CardUserKey,
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

    // A stored card's ending decides: 0008 (paid at first) is declined on renewal; the other paying endings pay.
    private GatewayAnswer Charge(string merchantId, GatewayCharge charge)
    {
        if (_bank.CardOf(merchantId, charge.CardUserKey, charge.CardToken) is not SimulatedCard card)
            return new GatewayAnswer(false, "card_not_found", "No such stored card.");
        if (_bank.ByOrder(merchantId, charge.OrderNo) is not null)
            return new GatewayAnswer(false, "duplicate_order", "This order number was already used.");
        string? error = card.Last4 == SimulatorControl.DeclinedOnRenewalEnding ? "card_declined" : null;
        SimulatedPayment payment = _bank.Add(token => new SimulatedPayment
        {
            Token = token,
            OrderNo = charge.OrderNo,
            MerchantId = merchantId,
            Amount = new Money(charge.AmountMinor, charge.Currency),
            CallbackUrl = "",
            ReturnUrl = "",
            Flow = SimulatorFlow.HostedForm,
            Description = charge.Description,
        });
        lock (_bank.Lock)
        {
            payment.CardLast4 = card.Last4;
            payment.State = error is null ? ProviderPaymentState.Paid : ProviderPaymentState.Failed;
            payment.PaymentId = _bank.NextId("SIM-P");
            payment.TransactionId = error is null ? _bank.NextId("SIM-T") : null;
        }
        return new GatewayAnswer(true, error, State: payment.State.ToString(), AmountMinor: charge.AmountMinor, Currency: charge.Currency,
            PaymentId: payment.PaymentId, TransactionId: payment.TransactionId);
    }

    private GatewayAnswer DeleteCard(string merchantId, GatewayCard card) =>
        _bank.RemoveCard(merchantId, card.CardUserKey, card.CardToken)
            ? new GatewayAnswer(true, Reference: _bank.NextId("SIM-D"))
            : new GatewayAnswer(false, "card_not_found", "No such stored card.");

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private static HttpResponseMessage Answer(HttpStatusCode status, GatewayAnswer answer) =>
        new(status) { Content = JsonContent.Create(answer, options: Json) };
}
