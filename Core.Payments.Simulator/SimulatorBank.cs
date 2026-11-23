using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

public enum SimulatorFlow
{
    // Like iyzico: a hosted payment page; the browser posts a signed result back.
    HostedForm,
    // Like PayTR: an embedded page; the provider posts a signed notification to the server and expects "OK".
    ServerNotification,
}

public sealed class SimulatorOptions
{
    /// <summary>The credentials' extra that picks the flow (<see cref="SimulatorFlow"/> name; HostedForm when absent).</summary>
    public const string FlowKey = "flow";

    /// <summary>Where the application serves the simulator's payment page; the page of a payment is <c>{PageBaseUrl}/{token}</c>.</summary>
    public string PageBaseUrl { get; set; } = "";
}

/// <summary>One payment as the simulated provider holds it.</summary>
public sealed class SimulatedPayment
{
    public required string Token { get; init; }
    public required string OrderNo { get; init; }
    public required string MerchantId { get; init; }
    public required Money Amount { get; init; }
    public required string CallbackUrl { get; init; }
    public required string ReturnUrl { get; init; }
    public required SimulatorFlow Flow { get; init; }
    public required string Description { get; init; }
    public ProviderPaymentState State { get; internal set; } = ProviderPaymentState.Pending;
    public string? PaymentId { get; internal set; }
    public string? TransactionId { get; internal set; }
    public string? CardLast4 { get; internal set; }
    public Money Refunded { get; internal set; }
    public bool DayClosed { get; internal set; }
    // A notification held back (card ending 0005) until the control sends it.
    internal IReadOnlyDictionary<string, string>? HeldNotification { get; set; }
}

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

/// <summary>The simulator's message signature: HMAC-SHA256 with the merchant's secret over the fixed fields, base64.</summary>
public static class SimulatorSignature
{
    public const string HashField = "hash";
    private static readonly string[] SignedFields =
        ["merchant_id", "order_no", "status", "total_amount", "currency", "payment_id", "transaction_id", "failed_reason_code"];

    public static string Compute(string secret, IReadOnlyDictionary<string, string> fields) =>
        Hmac(secret, string.Join("|", SignedFields.Select(f => fields.GetValueOrDefault(f) ?? "")));

    public static string Hmac(string secret, string text) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text)));

    public static bool Matches(string expected, string? actual) =>
        actual is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    internal static string Minor(Money amount) => amount.Minor.ToString(CultureInfo.InvariantCulture);
}
