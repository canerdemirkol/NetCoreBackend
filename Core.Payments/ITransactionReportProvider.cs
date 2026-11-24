namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>
/// E13-14b: a provider that reports a day's transactions (<see cref="PaymentCapabilities.TransactionReport"/>), for the application to
/// reconcile with its orders. The day is the provider's own (Turkey for iyzico and PayTR). Calls run in the caller's
/// <see cref="Logging.PaymentLogScope"/>.
/// </summary>
public interface ITransactionReportProvider : IPaymentProvider
{
    Task<TransactionReportResult> GetTransactionsAsync(DateOnly day, MerchantCredentials credentials, CancellationToken cancellationToken);
}
