# NetCoreBackend.NArchitecture.Core.Payments.PayTr (preview)

PayTR for `Core.Payments`: the iFrame API (PayTR's hosted payment page), written from dev.paytr.com. **Preview** until verified in a PayTR
store's test mode.

- `StartAsync`: gets an iFrame token (`/odeme/api/get-token`, signed `paytr_token`) and returns `https://www.paytr.com/odeme/guvenli/{token}`.
  The browser comes back to the request's `ReturnUrl` with no data; the result arrives as a notification.
- `VerifyNotificationAsync`: checks the notification's `hash` (no network call) and answers `OK`. The amount read is the order's own
  `payment_amount` (an instalment payment's `total_amount` includes the fee).
- `QueryAsync` (`/odeme/durum-sorgu`, with refunds), `RefundAsync` (`/odeme/iade`, full or partial). PayTR has no cancel.
- Credentials: `MerchantId` = merchant_id, `ApiKey` = merchant_key, `SecretKey` = merchant_salt. `PaymentEnvironment.Test` sends `test_mode=1`.
- The notification URL is set in the PayTR store panel (Destek & Kurulum › Ayarlar › Bildirim URL), not per request.
- PayTR's order number (`merchant_oid`) is letters and digits only, at most 64 characters.

**Addresses are configuration, not code.** `PayTrOptions` holds the documented address, paths and payment page as defaults; the
application overrides them (`AddPayTrPayments(o => …)`), and a store may carry its own base address in
`MerchantCredentials.Extra[ProviderBaseAddress.Key]` (HTTPS only). A change at PayTR needs no new package.

```csharp
services.AddPayTrPayments(o => configuration.GetSection("Payments:PayTr").Bind(o)); // needs an IPaymentLogWriter
```
