namespace NetCoreBackend.NArchitecture.Core.Payments.PayTr;

/// <summary>PayTR's addresses (dev.paytr.com). Test and live use the same ones; a test payment is marked with test_mode=1.</summary>
public static class PayTrEndpoints
{
    public static readonly Uri BaseAddress = new("https://www.paytr.com/");

    public const string GetToken = "/odeme/api/get-token";
    public const string StatusQuery = "/odeme/durum-sorgu";
    public const string Refund = "/odeme/iade";
    private const string PaymentPagePrefix = "https://www.paytr.com/odeme/guvenli/";

    /// <summary>The iFrame page of a token; the browser goes to it.</summary>
    public static string PaymentPage(string token) => PaymentPagePrefix + token;
}
