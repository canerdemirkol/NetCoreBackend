namespace NetCoreBackend.NArchitecture.Core.Security.Encryption;

// Strongly-typed wrapper around the 32-byte AES-256 master key used by
// AesGcmEncryptionHelper. Registering a bare `byte[]` as a DI Singleton conflicts with
// every other byte[] service in the graph; this type exists purely so the resolution
// site is unambiguous.
//
// Production: load the key from a secret store at startup, validate its length, register
// once as Singleton. Never bind from appsettings.json directly.
public sealed class EncryptionMasterKey
{
    // Stored as a private field, not as a record's auto-property, so the underlying buffer
    // is never handed out by reference. A defensive copy is taken on each Value read so
    // callers can't mutate the master key through the returned array.
    private readonly byte[] _value;

    public EncryptionMasterKey(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != AesGcmEncryptionHelper.KeySize)
            throw new ArgumentException(
                $"EncryptionMasterKey must be exactly {AesGcmEncryptionHelper.KeySize} bytes (was {value.Length}).",
                nameof(value));

        // Defensive copy IN so callers can clear their buffer without invalidating the key.
        _value = (byte[])value.Clone();
    }

    // Defensive copy OUT — caller mutations don't affect the stored key. The allocation cost
    // is negligible compared to the AES-GCM operation that immediately follows.
    public byte[] Value => (byte[])_value.Clone();

    // Allocation-free read for hot paths (e.g. AesGcm.Encrypt accepts ReadOnlySpan<byte>).
    public ReadOnlySpan<byte> AsSpan() => _value;
}
