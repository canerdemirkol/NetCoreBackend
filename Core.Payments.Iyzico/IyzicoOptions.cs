namespace NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

/// <summary>
/// iyzico's addresses and paths. The values here are docs.iyzico.com's, as defaults only: the application overrides them from its
/// configuration, and a merchant account may carry its own address (<see cref="ProviderBaseAddress"/>), so a change at iyzico needs no
/// new package. A test account calls the sandbox, a live one the live API.
/// </summary>
public sealed class IyzicoOptions
{
    public Uri SandboxBaseAddress { get; set; } = new("https://sandbox-api.iyzipay.com/");
    public Uri LiveBaseAddress { get; set; } = new("https://api.iyzipay.com/");
    public string CheckoutFormInitializePath { get; set; } = "/payment/iyzipos/checkoutform/initialize/auth/ecom";
    public string CheckoutFormResultPath { get; set; } = "/payment/iyzipos/checkoutform/auth/ecom/detail";
    public string PaymentDetailPath { get; set; } = "/payment/detail";
    public string CancelPath { get; set; } = "/payment/cancel";
    public string RefundByTransactionPath { get; set; } = "/payment/refund";
    public string RefundByPaymentPath { get; set; } = "/v2/payment/refund";
    public string StoredCardChargePath { get; set; } = "/payment/auth";
    public string StoredCardPath { get; set; } = "/cardstorage/card";

    public Uri BaseAddressFor(PaymentEnvironment environment) => environment == PaymentEnvironment.Live ? LiveBaseAddress : SandboxBaseAddress;
}
