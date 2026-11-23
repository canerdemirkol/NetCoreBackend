# NetCoreBackend.NArchitecture.Core.Payments.Simulator (preview)

A payment provider for development and tests. **Never for production:** registering it in the Production environment throws.

- Two flows, chosen per merchant (`SimulatorOptions.FlowKey` in the credentials' extras): **HostedForm** (a payment page; the browser posts
  a signed result back) and **ServerNotification** (a signed server-to-server notification answered "OK").
- Signatures are HMAC-SHA256 with the merchant's secret; provider calls go through an in-process gateway over HTTP, so they pass the same
  `PaymentLoggingHandler` as a real provider.
- Magic card endings: `0000` paid, `0002` declined, `0003` 3D failed, `0004` notification with another amount, `0005` notification held
  back, `0006` notification sent twice, `0007` tampered signature, `0008` paid but declined when charged again as a stored card; any
  other ending fails.
- Stored cards (`IStoredCardPaymentProvider`): a payment started with `StoreCard` keeps the card; charges and deletes go through the
  gateway like any call. Cards live in memory and are lost when the process ends.
- `ISimulatorControl`: render the payment page, submit a card, send a held notification (or twice, or tampered), close the day (cancel
  before, refund after; partial refunds cannot exceed the payment).

```csharp
if (!environment.IsProduction())
    services.AddPaymentSimulator(environment, o => o.PageBaseUrl = "https://localhost:5001/api/payments/simulator/pay");
```
