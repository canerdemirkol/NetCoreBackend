using System.Security.Cryptography;
using System.Text;
using NetCoreBackend.NArchitecture.Core.Security.Encryption;

namespace NetCoreBackend.NArchitecture.Core.Test.Security;

public sealed class AesGcmEncryptionHelperTests
{
    [Fact]
    public void RoundTrip_RecoversOriginal()
    {
        byte[] key = AesGcmEncryptionHelper.GenerateKey();
        byte[] plain = Encoding.UTF8.GetBytes("totp-secret-1234567890");

        byte[] encrypted = AesGcmEncryptionHelper.Encrypt(plain, key);
        byte[] decrypted = AesGcmEncryptionHelper.Decrypt(encrypted, key);

        Assert.Equal(plain, decrypted);
        Assert.NotEqual(plain, encrypted);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsAuthenticationTag()
    {
        byte[] key = AesGcmEncryptionHelper.GenerateKey();
        byte[] plain = Encoding.UTF8.GetBytes("payload");
        byte[] encrypted = AesGcmEncryptionHelper.Encrypt(plain, key);

        // Flip a bit in the ciphertext section (after nonce + tag).
        encrypted[encrypted.Length - 1] ^= 0x01;

        Assert.Throws<CryptographicException>(
            () => AesGcmEncryptionHelper.Decrypt(encrypted, key));
    }

    [Fact]
    public void Decrypt_WithDifferentKey_Fails()
    {
        byte[] keyA = AesGcmEncryptionHelper.GenerateKey();
        byte[] keyB = AesGcmEncryptionHelper.GenerateKey();
        byte[] encrypted = AesGcmEncryptionHelper.Encrypt(Encoding.UTF8.GetBytes("x"), keyA);

        Assert.Throws<CryptographicException>(
            () => AesGcmEncryptionHelper.Decrypt(encrypted, keyB));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Encrypt_WrongKeySize_Throws(int keyLength)
    {
        Assert.Throws<ArgumentException>(
            () => AesGcmEncryptionHelper.Encrypt(new byte[] { 1 }, new byte[keyLength]));
    }

    [Fact]
    public void AssociatedData_Mismatch_Fails()
    {
        byte[] key = AesGcmEncryptionHelper.GenerateKey();
        byte[] plain = Encoding.UTF8.GetBytes("payload");
        byte[] adA = Encoding.UTF8.GetBytes("user:1");
        byte[] adB = Encoding.UTF8.GetBytes("user:2");

        byte[] encrypted = AesGcmEncryptionHelper.Encrypt(plain, key, adA);
        Assert.Throws<CryptographicException>(
            () => AesGcmEncryptionHelper.Decrypt(encrypted, key, adB));
    }
}
