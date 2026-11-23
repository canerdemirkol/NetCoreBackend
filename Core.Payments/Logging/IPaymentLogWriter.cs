namespace NetCoreBackend.NArchitecture.Core.Payments.Logging;

/// <summary>
/// Stores log rows, append-only (the application: its own table, insert and select only). A failure throws: a payment step whose log cannot
/// be written does not go ahead.
/// </summary>
public interface IPaymentLogWriter
{
    Task WriteAsync(PaymentLogEntry entry, CancellationToken cancellationToken);
}
