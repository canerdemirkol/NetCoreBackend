using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>The simulator's message signature: HMAC-SHA256 with the merchant's secret over the fixed fields, base64.</summary>
public static class SimulatorSignature
{
    public const string HashField = "hash";
    private static readonly string[] SignedFields =
        ["merchant_id", "order_no", "status", "total_amount", "currency", "payment_id", "transaction_id", "failed_reason_code", "card_user_key",
            "card_token", "installment_fee"];

    public static string Compute(string secret, IReadOnlyDictionary<string, string> fields) =>
        Hmac(secret, string.Join("|", SignedFields.Select(f => fields.GetValueOrDefault(f) ?? "")));

    public static string Hmac(string secret, string text) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text)));

    public static bool Matches(string expected, string? actual) =>
        actual is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    internal static string Minor(Money amount) => amount.Minor.ToString(CultureInfo.InvariantCulture);
}
