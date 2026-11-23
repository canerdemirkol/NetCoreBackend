# NetCoreBackend.NArchitecture.Core.Payments.Iyzico (preview)

iyzico for `Core.Payments`: the Checkout Form (iyzico's hosted payment page) over the IYZWSv2 HMAC API, written from docs.iyzico.com.
**Preview** until verified in the iyzico sandbox.

- `StartAsync`: initializes a Checkout Form (one `VIRTUAL` basket item, the order number as `conversationId` and `basketId`) and returns its
  token and `paymentPageUrl`. The browser comes back to the request's `ReturnUrl` with the token.
- `VerifyNotificationAsync`: retrieves the form's result by token and believes it only when the answer's `signature` matches
  (HMAC-SHA256 of the documented fields). A webhook's `X-IYZ-SIGNATURE-V3` header is checked when present.
- `QueryAsync` (`/payment/detail`), `CancelAsync` (same day, whole amount), `RefundAsync` (by item transaction; by payment when the
  transaction id is unknown).
- Credentials: `MerchantId` = iyzico merchant id, `ApiKey`, `SecretKey`. `PaymentEnvironment.Test` calls only `sandbox-api.iyzipay.com`.
- The buyer needs a first and last name, e-mail, phone, address, city and country. iyzico requires an identity number: an 11-digit one is
  sent as given, anything else as `11111111111`.

```csharp
services.AddIyzicoPayments(); // needs an IPaymentLogWriter; every call is written to the payment log
```
