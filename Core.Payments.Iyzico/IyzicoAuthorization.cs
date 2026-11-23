using System.Security.Cryptography;
using System.Text;

namespace NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

/// <summary>
/// iyzico's IYZWSv2 request authentication (docs.iyzico.com, HMACSHA256 auth): encryptedData = hex HMAC-SHA256 of randomKey + URI path +
/// request body with the secret key; the header is "IYZWSv2 " + base64 of "apiKey:{apiKey}&amp;randomKey:{randomKey}&amp;signature:{encryptedData}".
/// The random key travels in the x-iyzi-rnd header.
/// </summary>
public static class IyzicoAuthorization
{
    public const string RandomKeyHeader = "x-iyzi-rnd";
    private const string Scheme = "IYZWSv2";

    public static string Header(string apiKey, string secretKey, string randomKey, string path, string body)
    {
        string encryptedData = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(randomKey + path + body)));
        string authorization = $"apiKey:{apiKey}&randomKey:{randomKey}&signature:{encryptedData}";
        return $"{Scheme} {Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization))}";
    }

    /// <summary>A fresh random key per request.</summary>
    public static string NewRandomKey() => $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{RandomNumberGenerator.GetInt32(100_000, 1_000_000)}";
}
