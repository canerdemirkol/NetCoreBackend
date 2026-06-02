using System.Security.Cryptography;
using System.Text;

namespace NetCoreBackend.NArchitecture.Core.Security.Hashing;

// Password hashing utility.
//
// Algorithm: PBKDF2-HMAC-SHA512, 210_000 iterations (OWASP 2024 minimum for SHA-512).
// HMACSHA512 fast-hashing was previously used here; it is unsuitable for password storage
// because a single HMAC operation per password makes GPU brute-force trivial against a leaked DB.
//
// Backward compatibility: <see cref="VerifyPasswordHash"/> auto-detects legacy HMACSHA512 hashes
// (recognized by 128-byte salt — the default HMACSHA512 block-size key) and verifies them with
// the legacy algorithm, so consuming applications can migrate users incrementally. New hashes
// use a 16-byte PBKDF2 salt. Use <see cref="IsLegacyHash"/> to detect old hashes after a
// successful login and re-hash with <see cref="CreatePasswordHash"/>.
public static class HashingHelper
{
    private const int Pbkdf2Iterations = 210_000;
    private const int Pbkdf2SaltSize = 16;
    private const int Pbkdf2HashSize = 64;
    private const int LegacyHmacSaltSize = 128;  // HMACSHA512.Key default size (block size)

    /// <summary>
    /// Create a password hash and salt with PBKDF2-HMAC-SHA512.
    /// </summary>
    public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password must not be null or empty.", nameof(password));

        passwordSalt = RandomNumberGenerator.GetBytes(Pbkdf2SaltSize);
        passwordHash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: passwordSalt,
            iterations: Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA512,
            outputLength: Pbkdf2HashSize);
    }

    /// <summary>
    /// Verify a password against the stored hash and salt. Constant-time comparison.
    /// Transparently handles both PBKDF2 (current) and HMACSHA512 (legacy) hashes.
    /// </summary>
    public static bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password must not be null or empty.", nameof(password));
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(passwordSalt);

        if (passwordSalt.Length == LegacyHmacSaltSize)
        {
            // Legacy HMACSHA512 path — kept for backward compatibility. Mark these rows for
            // upgrade via IsLegacyHash after a successful login.
            using HMACSHA512 hmac = new(passwordSalt);
            byte[] legacyComputed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(legacyComputed, passwordHash);
        }

        byte[] computed = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: passwordSalt,
            iterations: Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA512,
            outputLength: Pbkdf2HashSize);
        return CryptographicOperations.FixedTimeEquals(computed, passwordHash);
    }

    /// <summary>
    /// Returns true if the given salt belongs to the legacy HMACSHA512 hash format and should be
    /// re-hashed with <see cref="CreatePasswordHash"/> on the next successful login.
    /// </summary>
    public static bool IsLegacyHash(byte[] passwordSalt) =>
        passwordSalt is { Length: LegacyHmacSaltSize };
}
