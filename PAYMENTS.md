# Payments (preview)

`Core.Payments` and `Core.Payments.Simulator`, `1.0.0-preview.N` until each real provider is verified in its sandbox.

## What Core does and what the application does

| Core | Application |
|---|---|
| `IPaymentProvider` and its types; one package per provider (iyzico, PayTR later) | Merchant accounts (encrypted credentials), orders, receipts |
| `Money`, `VatSplit` | Prices, periods, proration (which edition, which tenant) |
| `PaymentOrderTransitions` (pure status rules) | The order table and the transaction around a status change |
| `PaymentDataMasker`, `PaymentLogEntry`, `PaymentLoggingHandler` | `IPaymentLogWriter`: an append-only table (insert and select only) |
| Simulator (development and tests) | Its routes (payment page, card submit, control) outside Production |

Providers are stateless: they know no database, order or tenant. Credentials come with each call; base addresses are fixed in the provider
package (never taken from a request).

## The flow

1. **Start.** The application computes the amount, stores the order as pending and commits; then, inside a `PaymentLogScope`, calls
   `StartAsync`. The result carries the provider token and the payment page (an address or a form's HTML).
2. **Pay.** The user pays on the provider's page; card data never reaches the application.
3. **Notification / return.** The application logs the raw message first, finds the order it names with `ReferenceOf` (order number or
   provider token, untrusted, no credentials needed), then `VerifyNotification` (signature with the order's own merchant account; no network) and `PaymentOrderTransitions.Decide` against what it expected (order number, amount, merchant).
   A paid notification that does not match goes to review; a repeated one has no effect; a bad signature is rejected.
4. **Reconcile.** Pending orders are asked with `QueryAsync`; cancel before the day closes, refund after (partial refunds cannot exceed
   the payment).
5. **Renew (stored card).** A provider with `PaymentCapabilities.Recurring` implements `IStoredCardPaymentProvider`. The first payment
   is started with `StoreCard` (and the customer's earlier `StoredCardOwnerKey`); its verified result names the stored card. At the
   period's end the application charges it with `ChargeStoredCardAsync` (no page, no 3D, answered at once: Paid or Failed is the
   provider's verdict, a failed call is unknown and is asked again with `QueryAsync`) and deletes it with `DeleteStoredCardAsync` when the
   customer turns renewal off. Log steps 21 and 22.
6. **Instalments and the daily report (E13-14b).** `NotificationVerification.Amount` and `PaymentStatusResult.Amount` are the order's own
   amount; what the customer paid above it for instalments is `InstallmentFee` (the fee's payer is the provider's panel setting). A
   provider with `PaymentCapabilities.TransactionReport` implements `ITransactionReportProvider`: a day's payments, cancels and refunds
   with commission and expected payout, for the application to reconcile. Log step 23.

## Periods and upgrades

`BillingCalendar.EndOf` gives a period's end (calendar month or year, anchor day kept, host time zone); `Proration.UpgradePrice` the
price of a dearer edition for the time left. The application decides what is sold (which edition, purchase or extension) and when.

## The payment transaction log

Every step is a row (`PaymentLogStep`, 20 steps: order created, consent, start request and response, page issued, page event, return,
notification, signature, status query, comparison, status change, our answer, subscription, e-mail, reconciliation, cancel/refund,
manual "paid", unmatched message, error). A provider's HTTP calls cannot miss the log:

```csharp
services.AddHttpClient<MyProvider>().AddPaymentLogging();   // PaymentLoggingHandler

using (PaymentLogScope.Begin(new PaymentLogContext
{
    OrderId = order.Id, Step = PaymentLogStep.StartRequestSent, ResponseStep = PaymentLogStep.StartResponseReceived,
    Provider = account.Provider, Environment = account.Environment, MerchantAccountId = account.Id,
    CredentialVersion = account.CredentialVersion, CredentialFingerprint = account.Fingerprint,
    SignatureHeaderNames = ["X-Signature"],
}))
{
    PaymentStartResult result = await provider.StartAsync(request, credentials, ct);
}
```

- A row is written **before** the request is sent; if it cannot be written the request is not sent (fail-closed).
- The response, or the failure (timeout, DNS, TLS), is a **second row**; nothing is updated.
- Headers: `Content-Type`, `User-Agent` by value; signature headers by presence; cookies and authorization never.
- Bodies go through `PaymentDataMasker`: card numbers removed, secrets `***`, personal data in `PersonalData`, apart from the body.
- A provider call outside a scope throws: there is no unlogged payment call.

## The simulator

```csharp
if (!environment.IsProduction())
    services.AddPaymentSimulator(environment, o => o.PageBaseUrl = $"{apiRoot}/api/payments/simulator/pay");
```

- **Flows** (per merchant, `Extra["flow"]`): `HostedForm` (the page posts a signed result to the return address, like iyzico) and
  `ServerNotification` (a signed notification is posted to the callback address and answered "OK", like PayTR).
- **Card endings:** `0000` paid · `0002` declined · `0003` 3D failed · `0004` notification with another amount · `0005` notification held
  back · `0006` notification sent twice · `0007` tampered signature · `0008` paid, declined when charged again as a stored card · anything
  else fails.
- **Stored cards:** a payment started with `StoreCard` keeps the card; the result carries `card_user_key` and `card_token` (signed). The
  books are in memory: a restarted simulator has no stored cards (`card_not_found`).
- **Control** (`ISimulatorControl`): render the page, submit a card, send a held notification (or twice, or tampered), close the day.
- Calls go to an in-process HTTP gateway (`https://simulator.payments.local/`), signed with the merchant's secret (the first secret a
  merchant uses; a later different secret is refused), so they pass `PaymentLoggingHandler` like a real provider's.
- `AddPaymentSimulator` throws in Production. State is in memory: one instance, development and tests only.
