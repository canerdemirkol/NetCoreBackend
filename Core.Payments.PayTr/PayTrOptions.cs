namespace NetCoreBackend.NArchitecture.Core.Payments.PayTr;

/// <summary>
/// PayTR's address, paths and payment page. The values here are dev.paytr.com's, as defaults only: the application overrides them from its
/// configuration, and a store may carry its own address (<see cref="ProviderBaseAddress"/>), so a change at PayTR needs no new package.
/// Test and live share the address; a test payment is marked with test_mode=1.
/// </summary>
public sealed class PayTrOptions
{
    public Uri BaseAddress { get; set; } = new("https://www.paytr.com/");
    public string GetTokenPath { get; set; } = "/odeme/api/get-token";
    public string StatusQueryPath { get; set; } = "/odeme/durum-sorgu";
    public string RefundPath { get; set; } = "/odeme/iade";
    public string TransactionReportPath { get; set; } = "/rapor/islem-dokumu";
    public string PaymentPagePrefix { get; set; } = "https://www.paytr.com/odeme/guvenli/";

    /// <summary>The iFrame page of a token; the browser goes to it.</summary>
    public string PaymentPage(string token) => PaymentPagePrefix + token;
}
