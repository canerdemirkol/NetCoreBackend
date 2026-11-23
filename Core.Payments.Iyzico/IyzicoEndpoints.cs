namespace NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

/// <summary>
/// iyzico's addresses (docs.iyzico.com). "The only difference between our LIVE and Sandbox ENVs is URL paths": a test account calls only the
/// sandbox, a live one only the live API.
/// </summary>
public static class IyzicoEndpoints
{
    public static readonly Uri SandboxBaseAddress = new("https://sandbox-api.iyzipay.com/");
    public static readonly Uri LiveBaseAddress = new("https://api.iyzipay.com/");

    public const string CheckoutFormInitialize = "/payment/iyzipos/checkoutform/initialize/auth/ecom";
    public const string CheckoutFormResult = "/payment/iyzipos/checkoutform/auth/ecom/detail";
    public const string PaymentDetail = "/payment/detail";
    public const string Cancel = "/payment/cancel";
    public const string RefundByTransaction = "/payment/refund";
    public const string RefundByPayment = "/v2/payment/refund";

    public static Uri BaseAddressFor(PaymentEnvironment environment) =>
        environment == PaymentEnvironment.Live ? LiveBaseAddress : SandboxBaseAddress;
}
