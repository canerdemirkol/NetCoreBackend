# NetCoreBackend.NArchitecture.Core.Payments.Iyzico (preview)

iyzico for `Core.Payments`: the Checkout Form (iyzico's hosted payment page) over the IYZWSv2 HMAC API, written from docs.iyzico.com.
**Preview** until verified in the iyzico sandbox.

- `StartAsync`: initializes a Checkout Form (one `VIRTUAL` basket item, the order number as `conversationId` and `basketId`) and returns its
  token and `paymentPageUrl`. The browser comes back to the request's `ReturnUrl` with the token.
- `VerifyNotificationAsync`: retrieves the form's result by token and believes it only when the answer's `signature` matches
  (HMAC-SHA256 of the documented fields). A webhook's `X-IYZ-SIGNATURE-V3` header is checked when present.
  A declined card comes back unsigned (`status: failure`, `paymentStatus: FAILURE`, a `paymentId`) and is read as a failed payment;
  `QueryAsync` reads it the same way.
- Stored cards (`IStoredCardPaymentProvider`): the Checkout Form result's `cardUserKey` and `cardToken` (when the merchant's card storage
  is on and the customer kept the card) come back as `StoredCard`; a start may pass the customer's `cardUserKey`. `ChargeStoredCardAsync`
  calls `/payment/auth` without 3D (`paymentGroup: SUBSCRIPTION`) and believes a paid answer only with its signature
  (`paymentId:currency:basketId:conversationId:paidPrice:price`); a declined card (status `failure` with a `paymentId`, unsigned) is a
  failed payment. `DeleteStoredCardAsync` calls `DELETE /cardstorage/card`. Verified in the sandbox (2026-11-23).
- Instalments: `Amount` is the basket `price`; what the customer paid above it (`paidPrice − price`, both signed) is `InstallmentFee`.
  Who pays the fee is set in iyzico's panel (Taksit ve Komisyon Ayarları), not in the request.
- `ITransactionReportProvider`: the daily report (`GET /v2/reporting/payment/transactions`, paged; IYZWSv2 over the path only):
  payments with commission and expected payout, cancels and refunds. Verified in the sandbox (2026-11-24).
- `QueryAsync` (`/payment/detail`), `CancelAsync` (same day, whole amount), `RefundAsync` (by item transaction; by payment when the
  transaction id is unknown).
- Credentials: `MerchantId` = iyzico merchant id, `ApiKey`, `SecretKey`. `PaymentEnvironment.Test` calls the sandbox address, `Live` the live one.
- The buyer needs a first and last name, e-mail, phone, address, city and country. iyzico requires an identity number: an 11-digit one is
  sent as given, anything else as `11111111111`.

**Addresses are configuration, not code.** `IyzicoOptions` holds the documented addresses and paths as defaults; the application overrides
them (`AddIyzicoPayments(o => …)`, e.g. bound from `appsettings.json`), and a merchant account may carry its own base address in
`MerchantCredentials.Extra[ProviderBaseAddress.Key]` (HTTPS only; never the live API on a test account). A change at iyzico needs no new
package.

```csharp
services.AddIyzicoPayments(o => configuration.GetSection("Payments:Iyzico").Bind(o)); // needs an IPaymentLogWriter
```
