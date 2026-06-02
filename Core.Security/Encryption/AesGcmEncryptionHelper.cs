using System.Security.Cryptography;

namespace NetCoreBackend.NArchitecture.Core.Security.Encryption;

/// <summary>
/// Symmetric at-rest encryption helper for short, security-sensitive payloads such as TOTP
/// secret keys, recovery codes or third-party API tokens that the framework must store
/// but not expose at row read.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Algorithm:</strong> AES-256-GCM (authenticated encryption). Output layout is a
/// single byte array: <c>[12-byte nonce][16-byte auth tag][ciphertext]</c>. Verification
/// fails with <see cref="CryptographicException"/> if any byte of the blob has been
/// tampered with, the key is wrong, or the associated data does not match.
/// </para>
/// <para>
/// <strong>Key management:</strong> caller supplies a 32-byte master key (AES-256). The key
/// SHOULD come from a secret manager (Azure Key Vault, AWS Secrets Manager, sealed
/// cluster secret) — not <c>appsettings.json</c>. Wrap the key in
/// <see cref="EncryptionMasterKey"/> when registering with DI to avoid <c>byte[]</c>
/// resolution conflicts.
/// </para>
/// <para>
/// <strong>Re-keying:</strong> keep the old key, decrypt-then-encrypt with the new key
/// during a one-time migration. Versioning is NOT baked into the blob — if you anticipate
/// key rotation, prefix the blob with your own version byte and switch keys based on that.
/// </para>
/// </remarks>
public static class AesGcmEncryptionHelper
{
    /// <summary>AES-256 key size in bytes.</summary>
    public const int KeySize = 32;
    /// <summary>GCM nonce size in bytes (RFC 5116 recommended).</summary>
    public const int NonceSize = 12;
    /// <summary>GCM authentication tag size in bytes (NIST SP 800-38D maximum).</summary>
    public const int TagSize = 16;

    /// <summary>
    /// Encrypt <paramref name="plaintext"/> with <paramref name="key"/> and an optional
    /// <paramref name="associatedData"/> binding. A fresh random nonce is generated on
    /// every call; never reuse one externally.
    /// </summary>
    /// <param name="plaintext">Bytes to encrypt. May be empty.</param>
    /// <param name="key">32-byte AES-256 key.</param>
    /// <param name="associatedData">Optional context bytes bound into the tag computation
    /// (e.g. <c>UTF8("otp:{userId}")</c>). The same value MUST be supplied to
    /// <see cref="Decrypt"/> or verification fails.</param>
    /// <returns>Self-contained blob: <c>[nonce | tag | ciphertext]</c>.</returns>
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

    /// <summary>
    /// Decrypt a blob produced by <see cref="Encrypt"/>. Tag verification is performed
    /// before the plaintext is returned — any tampering throws.
    /// </summary>
    /// <param name="blob">Self-contained blob: <c>[nonce | tag | ciphertext]</c>.</param>
    /// <param name="key">32-byte AES-256 key — must match the one used to encrypt.</param>
    /// <param name="associatedData">Must match the value supplied to <see cref="Encrypt"/>
    /// exactly; otherwise the tag mismatches and decryption fails.</param>
    /// <returns>The original plaintext bytes.</returns>
    /// <exception cref="CryptographicException">Tag mismatch — wrong key, mismatched
    /// associated data, or tampered ciphertext. The exception message includes blob length
    /// and associated-data presence to ease key-rotation debugging.</exception>
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

    /// <summary>
    /// Convenience: produce a cryptographically random 32-byte AES-256 key. Intended for
    /// one-off generation during deployment setup — the result should be stored in a secret
    /// manager and reused, not regenerated per run.
    /// </summary>
    public static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(KeySize);

    private static void GuardKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize)
            throw new ArgumentException($"AES-256 key must be exactly {KeySize} bytes (was {key.Length}).", nameof(key));
    }
}
