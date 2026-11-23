namespace NetCoreBackend.NArchitecture.Core.Payments.Logging;

/// <summary>
/// The ambient <see cref="PaymentLogContext"/> of the current async flow. Provider calls made without one are refused by
/// <see cref="PaymentLoggingHandler"/>: there is no unlogged payment call.
/// </summary>
public static class PaymentLogScope
{
    private static readonly AsyncLocal<PaymentLogContext?> Ambient = new();

    public static PaymentLogContext? Current => Ambient.Value;

    public static IDisposable Begin(PaymentLogContext context)
    {
        PaymentLogContext? previous = Ambient.Value;
        Ambient.Value = context;
        return new Restore(previous);
    }

    private sealed class Restore(PaymentLogContext? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}
