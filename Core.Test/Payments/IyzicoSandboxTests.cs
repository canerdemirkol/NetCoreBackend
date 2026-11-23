using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

namespace Core.Test.Payments;

/// <summary>
/// iyzico's sandbox, for real: the IYZWSv2 header and the request bodies are accepted and error answers are read. A payment itself needs
/// the hosted page (card form, 3D Secure), so paid flows and their signed answers are tried by hand; these cover what needs no card.
/// iyzico refuses e-mail addresses on reserved domains such as .local, so the buyer here uses example.com.
/// </summary>
public class IyzicoSandboxTests
{
    private static MerchantCredentials Sandbox() => new("sandbox", Environment.GetEnvironmentVariable(IyzicoSandboxFactAttribute.ApiKeyVariable)!,
        Environment.GetEnvironmentVariable(IyzicoSandboxFactAttribute.SecretKeyVariable)!, PaymentEnvironment.Test);

    private static IyzicoPaymentProvider Provider() => new(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

    private static string NewOrderNo() => "PO" + DateTime.UtcNow.ToString("yyyyMMdd") + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static PaymentStartRequest Start(string orderNo) => new(orderNo, Money.Of(100.5m),
        new PaymentBuyer("U-1", "Ayşe", "Yılmaz", "ayse@example.com", "+905551112233", "Atatürk Cd. 1", "İstanbul", "Turkey", "34000", null, "85.34.78.112"),
        "https://example.test/api/payments/callback/iyzico", "https://example.test/api/payments/return/iyzico", "Pro, aylık");

    [IyzicoSandboxFact]
    public async Task AStart_IsAccepted_AndGivesAPaymentPage()
    {
        PaymentStartResult started = await Provider().StartAsync(Start(NewOrderNo()), Sandbox(), CancellationToken.None);

        Assert.True(started.Succeeded, $"{started.ErrorCode}: {started.ErrorMessage}");
        Assert.False(string.IsNullOrEmpty(started.ProviderToken));
        Assert.StartsWith("https://", started.PaymentPageUrl);
    }

    // iyzico answers an unpaid form's token with an error (5122), so nothing is believed. A paid form's signed answer is tried by hand.
    [IyzicoSandboxFact]
    public async Task AnUnpaidForm_IsAnErrorAnswer_NeverReadAsPaid()
    {
        IyzicoPaymentProvider provider = Provider();
        PaymentStartResult started = await provider.StartAsync(Start(NewOrderNo()), Sandbox(), CancellationToken.None);
        Assert.True(started.Succeeded, $"{started.ErrorCode}: {started.ErrorMessage}");

        NotificationVerification verified = await provider.VerifyNotificationAsync(new IncomingPaymentMessage(
            new Dictionary<string, string> { ["token"] = started.ProviderToken! }, new Dictionary<string, string>()), Sandbox(), CancellationToken.None);

        Assert.False(verified.SignatureValid);
        Assert.Null(verified.Result);
        Assert.False(string.IsNullOrEmpty(verified.ErrorCode));
        Assert.NotEqual(IyzicoPaymentProvider.SignatureMismatchCode, verified.ErrorCode);
    }

    [IyzicoSandboxFact]
    public async Task AnUnknownOrder_IsAnErrorResult()
    {
        PaymentStatusResult status = await Provider().QueryAsync(new PaymentReference(NewOrderNo(), null, null), Sandbox(), CancellationToken.None);

        Assert.False(status.Succeeded);
        Assert.False(string.IsNullOrEmpty(status.ErrorCode));
    }
}
