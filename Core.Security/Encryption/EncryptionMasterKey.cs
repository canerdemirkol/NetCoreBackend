namespace NetCoreBackend.NArchitecture.Core.Security.Encryption;

/// <summary>
/// Strongly-typed wrapper around the 32-byte AES-256 master key used by
/// <see cref="AesGcmEncryptionHelper"/>. Exists primarily to disambiguate DI resolution
/// — a bare <c>byte[]</c> registered as a Singleton would conflict with every other
/// <c>byte[]</c> service in the graph.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Production:</strong> load the key from a secret store at startup (Azure Key
/// Vault, AWS Secrets Manager, sealed cluster secret), wrap it in this type, and register
/// once as Singleton. Never bind from <c>appsettings.json</c> directly.
/// </para>
/// <para>
/// <strong>Mutation safety:</strong> the underlying buffer is held in a private field and
/// defensively copied on both ingress (ctor) and egress (<see cref="Value"/> getter), so
/// neither the caller of the constructor nor a reader of <see cref="Value"/> can mutate
/// the stored master key. Use <see cref="AsSpan"/> for the allocation-free read path that
/// most <see cref="System.Security.Cryptography.AesGcm"/> overloads accept directly.
/// </para>
/// </remarks>
public sealed class EncryptionMasterKey
{
    private readonly byte[] _value;

    /// <summary>
    /// Construct from a 32-byte buffer. A defensive copy is taken so the caller may zero
    /// or reuse the input array without invalidating the master key.
    /// </summary>
    /// <param name="value">Exactly 32 bytes (AES-256).</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Length is not exactly
    /// <see cref="AesGcmEncryptionHelper.KeySize"/>.</exception>
    public EncryptionMasterKey(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != AesGcmEncryptionHelper.KeySize)
            throw new ArgumentException(
                $"EncryptionMasterKey must be exactly {AesGcmEncryptionHelper.KeySize} bytes (was {value.Length}).",
                nameof(value));

        _value = (byte[])value.Clone();
    }

    /// <summary>
    /// Returns a fresh 32-byte copy of the master key. Each read allocates — prefer
    /// <see cref="AsSpan"/> on hot paths.
    /// </summary>
    public byte[] Value => (byte[])_value.Clone();

    /// <summary>
    /// Allocation-free read-only view over the stored key for use with
    /// <c>Span</c>-accepting cryptography APIs.
    /// </summary>
    public ReadOnlySpan<byte> AsSpan() => _value;
}
