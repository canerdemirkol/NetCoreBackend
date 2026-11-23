using System.Security.Cryptography;
using System.Text;

namespace NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

/// <summary>
/// The signatures iyzico puts on its answers and webhooks (docs.iyzico.com: response signature validation, webhook). An answer's signature
/// is the hex HMAC-SHA256, with the secret key, of the documented fields joined with ":" (prices without trailing zeros). A webhook's
/// X-IYZ-SIGNATURE-V3 is the hex HMAC-SHA256 of secretKey + eventType + paymentId + token + conversationId + status.
/// </summary>
public static class IyzicoSignature
{
    public const string WebhookHeader = "X-IYZ-SIGNATURE-V3";
    private const string Separator = ":";

    public static string Compute(string secretKey, params string[] values) => Hmac(secretKey, string.Join(Separator, values));

    public static string Webhook(string secretKey, string eventType, string paymentId, string token, string conversationId, string status) =>
        Hmac(secretKey, secretKey + eventType + paymentId + token + conversationId + status);

    /// <summary>Compares in constant time; a missing signature never matches.</summary>
    public static bool Matches(string expected, string? given) =>
        given is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(given.ToLowerInvariant()));

    private static string Hmac(string secretKey, string message) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(message)));
}
