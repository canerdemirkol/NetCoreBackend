using MailKit.Net.Smtp;
using MimeKit;
using MimeKit.Cryptography;
using NetCoreBackend.NArchitecture.Core.Mailing;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;

namespace NetCoreBackend.NArchitecture.Core.Mailing.MailKit;

public class MailKitMailService : IMailService
{
    private readonly MailSettings _mailSettings;

    public MailKitMailService(MailSettings configuration)
    {
        _mailSettings = configuration;
    }

    public void SendMail(Mail mail)
    {
        if (mail.ToList == null || mail.ToList.Count < 1)
            return;
        emailPrepare(mail, email: out MimeMessage email, smtp: out SmtpClient smtp);
        using (email)
        using (smtp)
        {
            try
            {
                smtp.Send(email);
            }
            finally
            {
                if (smtp.IsConnected)
                    smtp.Disconnect(quit: true);
            }
        }
    }

    public async Task SendEmailAsync(Mail mail)
    {
        if (mail.ToList == null || mail.ToList.Count < 1)
            return;
        emailPrepare(mail, email: out MimeMessage email, smtp: out SmtpClient smtp);
        using (email)
        using (smtp)
        {
            try
            {
                await smtp.SendAsync(email);
            }
            finally
            {
                if (smtp.IsConnected)
                    await smtp.DisconnectAsync(quit: true);
            }
        }
    }

    private void emailPrepare(Mail mail, out MimeMessage email, out SmtpClient smtp)
    {
        email = new MimeMessage();
        email.From.Add(new MailboxAddress(_mailSettings.SenderFullName, _mailSettings.SenderEmail));
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

        if (_mailSettings.DkimPrivateKey != null && _mailSettings.DkimSelector != null && _mailSettings.DomainName != null)
        {
            DkimSigner signer =
                new(key: readPrivateKeyFromPemEncodedString(), _mailSettings.DomainName, _mailSettings.DkimSelector)
                {
                    HeaderCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Simple,
                    BodyCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Simple,
                    AgentOrUserIdentifier = $"@{_mailSettings.DomainName}",
                    QueryMethod = "dns/txt"
                };
            HeaderId[] headers = { HeaderId.From, HeaderId.Subject, HeaderId.To };
            signer.Sign(email, headers);
        }

        smtp = new SmtpClient();
        smtp.Connect(_mailSettings.Server, _mailSettings.Port);
        if (_mailSettings.AuthenticationRequired)
            smtp.Authenticate(_mailSettings.UserName, _mailSettings.Password);
    }

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
