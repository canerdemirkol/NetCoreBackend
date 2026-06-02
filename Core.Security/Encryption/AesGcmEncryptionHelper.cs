using System.Security.Cryptography;

namespace NetCoreBackend.NArchitecture.Core.Security.Encryption;

// Symmetric at-rest encryption helper for short, security-sensitive payloads such as TOTP
// secret keys, recovery codes, or third-party API tokens that the framework must store but
// not expose at row read.
//
// Algorithm: AES-GCM (authenticated encryption). Output layout is a single byte[]:
//   [12-byte nonce][16-byte auth tag][ciphertext]
// Verification fails (CryptographicException) if anyone tampers with any byte of the blob.
//
// Key management:
//   - Caller supplies a 32-byte master key (AES-256). The key SHOULD come from a secret
//     manager (Azure Key Vault, AWS Secrets Manager, sealed cluster secret) — not config.
//   - Re-keying: keep the old key, decrypt-then-encrypt with the new key during a one-time
//     migration. Versioning is NOT baked into the blob — if you anticipate key rotation,
//     prefix the blob with your own version byte and switch keys based on that.
public static class AesGcmEncryptionHelper
{
    public const int KeySize = 32;        // AES-256
    public const int NonceSize = 12;      // GCM standard
    public const int TagSize = 16;        // GCM standard

    public static byte[] Encrypt(byte[] plaintext, byte[] key, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        GuardKey(key);

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];

        using AesGcm aes = new(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        byte[] output = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, output, NonceSize + TagSize, ciphertext.Length);
        return output;
    }

    public static byte[] Decrypt(byte[] blob, byte[] key, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(blob);
        GuardKey(key);
        if (blob.Length < NonceSize + TagSize)
            throw new ArgumentException($"Encrypted blob is too short: {blob.Length} bytes (min {NonceSize + TagSize}).", nameof(blob));

        ReadOnlySpan<byte> nonce = blob.AsSpan(0, NonceSize);
        ReadOnlySpan<byte> tag = blob.AsSpan(NonceSize, TagSize);
        ReadOnlySpan<byte> ciphertext = blob.AsSpan(NonceSize + TagSize);

        byte[] plaintext = new byte[ciphertext.Length];
        using AesGcm aes = new(key, TagSize);
        try
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            // Default tag-mismatch message contains zero context. In production the three
            // realistic causes are: (a) wrong key (often during key rotation), (b) wrong
            // associatedData binding (e.g. blob loaded with different user/tenant scope),
            // (c) blob tampered or truncated at rest. Surface enough metadata to triage
            // without leaking secret material.
            throw new CryptographicException(
                $"AES-GCM decryption failed: authentication tag mismatch. " +
                $"Likely causes: wrong key (rotation in progress?), mismatched associatedData, or tampered ciphertext. " +
                $"Blob length: {blob.Length} bytes, associatedData present: {(associatedData is not null ? "yes" : "no")}.",
                ex);
        }
        return plaintext;
    }

    public static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(KeySize);

    private static void GuardKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize)
            throw new ArgumentException($"AES-256 key must be exactly {KeySize} bytes (was {key.Length}).", nameof(key));
    }
}
