using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>
/// The card endings: 0000 paid, 0002 declined, 0003 3D failed, 0004 paid but the notification says another amount, 0005 notification
/// held back, 0006 notification sent twice, 0007 tampered signature, 0008 paid but declined when the stored card is charged again; any
/// other ending fails ("unknown_test_card"). E13-14b: 0009 pays in 3 instalments with a 10% fee when the start allows 3 or more (else
/// in one).
/// </summary>
public sealed class SimulatorControl : ISimulatorControl
{
    internal const string DeclinedOnRenewalEnding = "0008";
    internal const string InstalmentEnding = "0009";
    private const int Instalments = 3;
    private const int FeePercent = 10;
    private const int Percent = 100;
    private const int AmountDifferenceMinor = 100;
    private const string TamperedHash = "tampered";
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly SimulatorBank _bank;
    private readonly SimulatorOptions _options;
    private readonly ISimulatorNotificationSender _sender;

    public SimulatorControl(SimulatorBank bank, SimulatorOptions options, ISimulatorNotificationSender sender)
    {
        _bank = bank;
        _options = options;
        _sender = sender;
    }

    public string RenderPaymentPage(string token)
    {
        SimulatedPayment payment = _bank.ByToken(token) ?? throw new KeyNotFoundException("Unknown simulator token.");
        string action = Html.Encode($"{_options.PageBaseUrl.TrimEnd('/')}/{token}");
        return "<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<title>Ödeme simülatörü</title></head>"
            + "<body style=\"font-family:'Segoe UI',Arial,sans-serif;background:#f3f5f8;margin:0;padding:24px\">"
            + "<main style=\"max-width:420px;margin:0 auto;background:#fff;border:1px solid #e3e8ef;border-radius:12px;padding:24px\">"
            + "<p style=\"margin:0 0 4px;font-size:12px;color:#b45309;font-weight:600\">TEST · Ödeme simülatörü · gerçek para çekilmez</p>"
            + $"<h1 style=\"margin:0 0 4px;font-size:20px\">{Html.Encode(payment.Amount.ToString())}</h1>"
            + $"<p style=\"margin:0 0 16px;color:#5b6676\">{Html.Encode(payment.Description)} · {Html.Encode(payment.OrderNo)}</p>"
            + $"<form method=\"post\" action=\"{action}\"><label for=\"card\" style=\"font-size:13px\">Kart numarası</label>"
            + "<input id=\"card\" name=\"cardNumber\" value=\"4000000000000000\" inputmode=\"numeric\" style=\"display:block;width:100%;box-sizing:border-box;padding:10px;margin:4px 0 12px;font-size:16px\">"
            + "<button type=\"submit\" style=\"width:100%;padding:12px;background:#1f4fd1;color:#fff;border:0;border-radius:8px;font-size:15px\">Öde</button></form>"
            + "<p style=\"font-size:12px;color:#5b6676;line-height:1.6\">Son dört hane sonucu seçer: 0000 başarılı · 0002 ret · 0003 3D başarısız · "
            + "0004 farklı tutar · 0005 gecikmeli bildirim · 0006 çift bildirim · 0007 bozuk imza · 0008 başarılı, saklı kartla yenilemede ret.</p>"
            + "</main></body></html>";
    }

    public async Task<SimulatorSubmitResult> SubmitCardAsync(string token, string cardNumber, CancellationToken cancellationToken)
    {
        SimulatedPayment payment = _bank.ByToken(token) ?? throw new KeyNotFoundException("Unknown simulator token.");
        string digits = new(cardNumber.Where(char.IsDigit).ToArray());
        string last4 = digits.Length >= 4 ? digits[^4..] : digits;
        (bool paid, string? error) = last4 switch
        {
            "0000" or "0004" or "0005" or "0006" or "0007" or DeclinedOnRenewalEnding or InstalmentEnding => (true, (string?)null),
            "0002" => (false, "card_declined"),
            "0003" => (false, "3ds_failed"),
            _ => (false, "unknown_test_card"),
        };
        Dictionary<string, string> message;
        lock (_bank.Lock)
        {
            if (payment.State != ProviderPaymentState.Pending)
                throw new InvalidOperationException("This simulated payment was already submitted.");
            payment.State = paid ? ProviderPaymentState.Paid : ProviderPaymentState.Failed;
            payment.CardLast4 = last4;
            if (paid)
            {
                payment.PaymentId = _bank.NextId("SIM-P");
                payment.TransactionId = _bank.NextId("SIM-T");
                payment.PaidAtUtc = DateTime.UtcNow;
                if (last4 == InstalmentEnding && payment.MaxInstallment >= Instalments)
                {
                    payment.Installment = Instalments;
                    payment.FeeMinor = payment.Amount.Minor * FeePercent / Percent;
                }
                if (payment.StoreCard)
                {
                    payment.OwnerKey ??= _bank.NextId("SIM-U");
                    payment.StoredCardToken = _bank.NextId("SIM-CARD");
                    _bank.AddCard(new SimulatedCard(payment.MerchantId, payment.OwnerKey, payment.StoredCardToken, last4));
                }
            }
            message = Message(payment, error, last4 == "0004" ? AmountDifferenceMinor : 0, tamper: last4 == "0007");
        }

        if (payment.Flow == SimulatorFlow.HostedForm)
            return Return(payment.ReturnUrl, message);

        int times = last4 == "0006" ? 2 : 1;
        if (last4 == "0005")
            payment.HeldNotification = message;
        else
            for (int i = 0; i < times; i++)
                await _sender.SendAsync(payment.CallbackUrl, message, cancellationToken);
        return Return(payment.ReturnUrl, new Dictionary<string, string> { ["order_no"] = payment.OrderNo });
    }

    public async Task SendNotificationAsync(string orderNo, int times, bool tamper, CancellationToken cancellationToken)
    {
        SimulatedPayment payment = _bank.All().FirstOrDefault(p => p.OrderNo == orderNo) ?? throw new KeyNotFoundException("Unknown simulator order.");
        IReadOnlyDictionary<string, string> message = payment.HeldNotification
            ?? Message(payment, payment.State == ProviderPaymentState.Failed ? "card_declined" : null, 0, tamper: false);
        if (tamper)
            message = new Dictionary<string, string>(message) { [SimulatorSignature.HashField] = TamperedHash };
        payment.HeldNotification = null;
        for (int i = 0; i < times; i++)
            await _sender.SendAsync(payment.CallbackUrl, message, cancellationToken);
    }

    public void CloseDay()
    {
        lock (_bank.Lock)
            foreach (SimulatedPayment payment in _bank.All())
                payment.DayClosed = true;
    }

    private Dictionary<string, string> Message(SimulatedPayment payment, string? error, long amountDifference, bool tamper)
    {
        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = payment.MerchantId,
            ["order_no"] = payment.OrderNo,
            ["status"] = error is null ? "success" : "failed",
            ["total_amount"] = (payment.Amount.Minor + amountDifference).ToString(CultureInfo.InvariantCulture),
            ["currency"] = payment.Amount.Currency,
            ["payment_id"] = payment.PaymentId ?? "",
            ["transaction_id"] = payment.TransactionId ?? "",
            ["failed_reason_code"] = error ?? "",
            ["card_last4"] = payment.CardLast4 ?? "",
            ["card_family"] = "Visa",
            ["flow"] = payment.Flow.ToString(),
        };
        fields["installment_count"] = payment.Installment.ToString(CultureInfo.InvariantCulture);
        if (payment.FeeMinor > 0)
            fields["installment_fee"] = payment.FeeMinor.ToString(CultureInfo.InvariantCulture);
        if (payment.StoredCardToken is not null)
        {
            fields["card_user_key"] = payment.OwnerKey!;
            fields["card_token"] = payment.StoredCardToken;
        }
        string secret = _bank.SecretOf(payment.MerchantId) ?? "";
        fields[SimulatorSignature.HashField] = tamper ? TamperedHash : SimulatorSignature.Compute(secret, fields);
        return fields;
    }

    private static SimulatorSubmitResult Return(string returnUrl, IReadOnlyDictionary<string, string> fields)
    {
        var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><title>Yönlendiriliyor</title></head>")
            .Append("<body onload=\"document.forms[0].submit()\">")
            .Append($"<form method=\"post\" action=\"{Html.Encode(returnUrl)}\">");
        foreach ((string name, string value) in fields)
            html.Append($"<input type=\"hidden\" name=\"{Html.Encode(name)}\" value=\"{Html.Encode(value)}\">");
        html.Append("<noscript><button type=\"submit\">Devam</button></noscript></form></body></html>");
        return new SimulatorSubmitResult(returnUrl, fields, html.ToString());
    }
}
