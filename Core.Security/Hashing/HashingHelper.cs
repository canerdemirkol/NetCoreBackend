using System.Security.Cryptography;
using System.Text;

namespace NetCoreBackend.NArchitecture.Core.Security.Hashing;

public static class HashingHelper
{
    /// <summary>
    /// Create a password hash and salt via HMACSHA512.
    /// </summary>
    public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using HMACSHA512 hmac = new();

        passwordSalt = hmac.Key;
        passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
    }

    /// <summary>
    /// Verify a password hash and salt via HMACSHA512.
    /// </summary>
    public static bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
    {
        using HMACSHA512 hmac = new(passwordSalt);

        byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        // FixedTimeEquals runs in O(len) regardless of where the first byte differs —
        // SequenceEqual short-circuits and leaks hash bytes through response timing.
        return CryptographicOperations.FixedTimeEquals(computedHash, passwordHash);
    }
}
