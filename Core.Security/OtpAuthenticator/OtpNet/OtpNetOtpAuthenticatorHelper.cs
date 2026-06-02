using OtpNet;

namespace NetCoreBackend.NArchitecture.Core.Security.OtpAuthenticator.OtpNet;

public class OtpNetOtpAuthenticatorHelper : IOtpAuthenticatorHelper
{
    public Task<byte[]> GenerateSecretKey()
    {
        byte[] key = KeyGeneration.GenerateRandomKey(20);

        string base32String = Base32Encoding.ToString(key);
        byte[] base32Bytes = Base32Encoding.ToBytes(base32String);

        return Task.FromResult(base32Bytes);
    }

    public Task<string> ConvertSecretKeyToString(byte[] secretKey)
    {
        string base32String = Base32Encoding.ToString(secretKey);
        return Task.FromResult(base32String);
    }

    public Task<bool> VerifyCode(byte[] secretKey, string code)
    {
        Totp totp = new(secretKey);

        // Accept codes from the previous and next 30s window to tolerate clock skew and
        // the common UX race where the user submits a code at the very edge of its validity.
        // RFC 6238 §5.2 explicitly recommends a small look-ahead/look-back window.
        VerificationWindow window = new(previous: 1, future: 1);
        bool result = totp.VerifyTotp(DateTime.UtcNow, code, out _, window);
        return Task.FromResult(result);
    }
}
