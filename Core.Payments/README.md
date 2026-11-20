# NetCoreBackend.NArchitecture.Core.Payments (preview)

Provider-independent payment building blocks for a virtual POS integration. **Preview** until each provider is verified in its sandbox.

- `IPaymentProvider`: start a payment (hosted page or token), verify a notification or return (signature; no network), query the status,
  cancel, refund (full or partial). Stateless: credentials come with every call; base addresses are fixed in the provider package.
- `Money` (minor units, one currency) and `VatSplit` (VAT-inclusive and VAT-exclusive prices; net + VAT = total, rounded away from zero).
- `PaymentOrderTransitions`: the order status table and the decision for an incoming notification (paid, failed, review on any mismatch of
  amount, currency, order number or merchant, no effect when repeated, rejected when the signature is bad).
- `PaymentDataMasker`: removes card numbers (Luhn), stars secrets, moves personal data out of the body.
- Payment transaction log: `PaymentLogEntry`, `PaymentLogStep` (20 steps), `IPaymentLogWriter` (the application stores entries,
  append-only). `PaymentLoggingHandler` logs every provider HTTP call before sending and after the answer or failure; a call outside a
  `PaymentLogScope`, or one whose log cannot be written, is not sent.

```csharp
services.AddHttpClient<MyProvider>().AddPaymentLogging();

using (PaymentLogScope.Begin(new PaymentLogContext { OrderId = order.Id, Step = PaymentLogStep.StartRequestSent,
                                                     ResponseStep = PaymentLogStep.StartResponseReceived, Provider = provider.Name }))
    result = await provider.StartAsync(request, credentials, ct);
```

See `PAYMENTS.md` in the repository for the flow, the log steps and the simulator.
