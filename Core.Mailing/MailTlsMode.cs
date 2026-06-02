namespace NetCoreBackend.NArchitecture.Core.Mailing;

// TLS mode used by the SMTP client. Mirrors MailKit.Security.SecureSocketOptions so consumers
// can configure transport security without taking a direct dependency on MailKit.
public enum MailTlsMode
{
    None = 0,
    Auto = 1,
    SslOnConnect = 2,
    StartTls = 3,
    StartTlsWhenAvailable = 4
}
