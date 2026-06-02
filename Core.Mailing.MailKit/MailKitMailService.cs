using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Cryptography;
using NetCoreBackend.NArchitecture.Core.Mailing;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;

namespace NetCoreBackend.NArchitecture.Core.Mailing.MailKit;

public class MailKitMailService : IMailService
{
    private readonly MailSettings _mailSettings;
    // PEM parsing via BouncyCastle is CPU-bound and the key never changes for the life of the
    // service. Cache the parsed key behind a Lazy so we pay the parse cost at most once.
    private readonly Lazy<AsymmetricKeyParameter?> _dkimKey;

    public MailKitMailService(MailSettings configuration)
    {
        _mailSettings = configuration;
        _dkimKey = new Lazy<AsymmetricKeyParameter?>(
            () => _mailSettings.DkimPrivateKey is null ? null : readPrivateKeyFromPemEncodedString(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public void SendMail(Mail mail)
    {
        if (!HasAnyRecipient(mail))
            return;

        MimeMessage email = buildMessage(mail);
        using (email)
        using (SmtpClient smtp = new())
        {
            try
            {
                smtp.Connect(_mailSettings.Server, _mailSettings.Port, mapTlsMode(_mailSettings.TlsMode));
                if (_mailSettings.AuthenticationRequired)
                    smtp.Authenticate(_mailSettings.UserName, _mailSettings.Password);
                smtp.Send(email);
            }
            finally
            {
                if (smtp.IsConnected)
                    smtp.Disconnect(quit: true);
            }
        }
    }

    public async Task SendEmailAsync(Mail mail, CancellationToken cancellationToken = default)
    {
        if (!HasAnyRecipient(mail))
            return;

        MimeMessage email = buildMessage(mail);
        using (email)
        using (SmtpClient smtp = new())
        {
            try
            {
                await smtp.ConnectAsync(_mailSettings.Server, _mailSettings.Port, mapTlsMode(_mailSettings.TlsMode), cancellationToken);
                if (_mailSettings.AuthenticationRequired)
                    await smtp.AuthenticateAsync(_mailSettings.UserName, _mailSettings.Password, cancellationToken);
                await smtp.SendAsync(email, cancellationToken);
            }
            finally
            {
                if (smtp.IsConnected)
                    await smtp.DisconnectAsync(quit: true, cancellationToken);
            }
        }
    }

    private MimeMessage buildMessage(Mail mail)
    {
        // SMTP header injection: a CR/LF in any user-controlled header value breaks out of the
        // header context and lets an attacker forge additional headers (e.g. extra BCC).
        // Subject and the List-Unsubscribe value are the realistic injection vectors here;
        // recipient addresses go through MimeKit's MailboxAddress parser which rejects them.
        GuardNoNewlines(mail.Subject, nameof(mail.Subject));
        GuardNoNewlines(mail.UnsubscribeLink, nameof(mail.UnsubscribeLink));

        MimeMessage email = new();
        email.From.Add(new MailboxAddress(_mailSettings.SenderFullName, _mailSettings.SenderEmail));
        if (mail.ToList != null && mail.ToList.Any())
            email.To.AddRange(mail.ToList);
        if (mail.CcList != null && mail.CcList.Any())
            email.Cc.AddRange(mail.CcList);
        if (mail.BccList != null && mail.BccList.Any())
            email.Bcc.AddRange(mail.BccList);

        email.Subject = mail.Subject;
        if (mail.UnsubscribeLink != null)
            email.Headers.Add(field: "List-Unsubscribe", value: $"<{mail.UnsubscribeLink}>");

        BodyBuilder bodyBuilder = new() { TextBody = mail.TextBody, HtmlBody = mail.HtmlBody };
        if (mail.Attachments != null)
            foreach (MimeEntity? attachment in mail.Attachments)
                if (attachment != null)
                    bodyBuilder.Attachments.Add(attachment);

        email.Body = bodyBuilder.ToMessageBody();
        email.Prepare(EncodingConstraint.SevenBit);

        if (_dkimKey.Value is not null && _mailSettings.DkimSelector != null && _mailSettings.DomainName != null)
        {
            DkimSigner signer =
                new(key: _dkimKey.Value, _mailSettings.DomainName, _mailSettings.DkimSelector)
                {
                    HeaderCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Simple,
                    BodyCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Simple,
                    AgentOrUserIdentifier = $"@{_mailSettings.DomainName}",
                    QueryMethod = "dns/txt"
                };
            HeaderId[] headers = { HeaderId.From, HeaderId.Subject, HeaderId.To };
            signer.Sign(email, headers);
        }

        return email;
    }

    // A mail is sendable if it has at least one recipient across To/Cc/Bcc. Previously the
    // short-circuit only looked at ToList, so a Bcc-only send (e.g. broadcast notifications)
    // was silently dropped.
    private static bool HasAnyRecipient(Mail mail) =>
        (mail.ToList?.Count ?? 0) + (mail.CcList?.Count ?? 0) + (mail.BccList?.Count ?? 0) > 0;

    private static void GuardNoNewlines(string? value, string fieldName)
    {
        if (value is null) return;
        if (value.AsSpan().IndexOfAny('\r', '\n') >= 0)
            throw new ArgumentException(
                $"Mail field '{fieldName}' must not contain CR/LF characters (header injection blocked).",
                fieldName);
    }

    private static SecureSocketOptions mapTlsMode(MailTlsMode mode) => mode switch
    {
        MailTlsMode.None => SecureSocketOptions.None,
        MailTlsMode.Auto => SecureSocketOptions.Auto,
        MailTlsMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
        MailTlsMode.StartTls => SecureSocketOptions.StartTls,
        MailTlsMode.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
        _ => SecureSocketOptions.StartTlsWhenAvailable
    };

    private AsymmetricKeyParameter readPrivateKeyFromPemEncodedString()
    {
        string pemEncodedKey =
            "-----BEGIN RSA PRIVATE KEY-----\n" + _mailSettings.DkimPrivateKey + "\n-----END RSA PRIVATE KEY-----";
        using StringReader stringReader = new(pemEncodedKey);
        PemReader pemReader = new(stringReader);
        object? pemObject = pemReader.ReadObject();

        if (pemObject is not AsymmetricCipherKeyPair keyPair)
            throw new InvalidOperationException(
                "DKIM private key could not be parsed as a PEM-encoded RSA key pair. " +
                "Check MailSettings.DkimPrivateKey for malformed or non-RSA key content.");

        return keyPair.Private;
    }
}
