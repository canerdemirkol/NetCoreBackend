namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>
/// The simulated provider's books, in memory (development and tests run one instance). A merchant's secret is the one it first used (trust
/// on first use), so a later call with another secret is refused like a real provider refuses a bad signature.
/// </summary>
public sealed class SimulatorBank
{
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _secrets = [];
    private readonly Dictionary<string, SimulatedPayment> _byToken = [];
    private readonly Dictionary<string, SimulatedPayment> _byOrder = [];
    private const int RunIdLength = 8;
    // The books are lost when the process ends and the sequence starts again; the run id keeps ids unique across restarts, since an
    // application keeps a provider's payment id unique per merchant account.
    private readonly string _runId = Guid.NewGuid().ToString("N")[..RunIdLength];
    private int _sequence;

    internal void EnsureMerchant(string merchantId, string secret)
    {
        lock (_lock)
            _secrets.TryAdd(merchantId, secret);
    }

    internal string? SecretOf(string merchantId)
    {
        lock (_lock)
            return _secrets.GetValueOrDefault(merchantId);
    }

    internal SimulatedPayment Add(Func<string, SimulatedPayment> create)
    {
        lock (_lock)
        {
            SimulatedPayment payment = create($"SIM-TOKEN-{++_sequence:D6}-{Guid.NewGuid():N}"[..32]);
            _byToken[payment.Token] = payment;
            _byOrder[Key(payment.MerchantId, payment.OrderNo)] = payment;
            return payment;
        }
    }

    internal string NextId(string prefix)
    {
        lock (_lock)
            return $"{prefix}-{_runId}-{++_sequence:D6}";
    }

    public SimulatedPayment? ByToken(string token)
    {
        lock (_lock)
            return _byToken.GetValueOrDefault(token);
    }

    public SimulatedPayment? ByOrder(string merchantId, string orderNo)
    {
        lock (_lock)
            return _byOrder.GetValueOrDefault(Key(merchantId, orderNo));
    }

    internal IReadOnlyList<SimulatedPayment> All()
    {
        lock (_lock)
            return [.. _byToken.Values];
    }

    internal object Lock => _lock;

    private static string Key(string merchantId, string orderNo) => $"{merchantId}\n{orderNo}";
}
