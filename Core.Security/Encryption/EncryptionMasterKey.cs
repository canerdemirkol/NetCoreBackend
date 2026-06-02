namespace NetCoreBackend.NArchitecture.Core.Security.Encryption;

// Strongly-typed wrapper around the 32-byte AES-256 master key used by
// AesGcmEncryptionHelper. Registering a bare `byte[]` as a DI Singleton conflicts with
// every other byte[] service in the graph; this record exists purely so the resolution
// site is unambiguous.
//
// Production: load the key from a secret store at startup, validate its length, register
// once as Singleton. Never bind from appsettings.json directly.
public sealed record EncryptionMasterKey
{
    public byte[] Value { get; }

    public EncryptionMasterKey(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != AesGcmEncryptionHelper.KeySize)
            throw new ArgumentException(
                $"EncryptionMasterKey must be exactly {AesGcmEncryptionHelper.KeySize} bytes (was {value.Length}).",
                nameof(value));

        // Defensive copy so callers can clear their buffer without invalidating the key.
        Value = (byte[])value.Clone();
    }
}
