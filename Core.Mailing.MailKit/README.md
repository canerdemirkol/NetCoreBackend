# Core.Mailing.MailKit

MailKit-based SMTP implementation of `IMailService`. Includes DKIM signing support.

## Installation

```csharp
// Program.cs
builder.Services.Configure<MailSettings>(config.GetSection("MailSettings"));
builder.Services.AddTransient<IMailService, MailKitMailService>();
```

## DKIM Signing

DKIM verifies that the emails sent genuinely originate from the domain. It helps avoid getting caught by spam filters.

```json
{
  "MailSettings": {
    "DkimPrivateKey": "-----BEGIN RSA PRIVATE KEY-----\n...",
    "DkimSelector": "mail",
    "DomainName": "myapp.com"
  }
}
```

If the DKIM information is left empty, signing is skipped.

## SMTP Connection Flow

```
ConnectAsync(server, port, mapTlsMode(MailSettings.TlsMode), cancellationToken)
  → AuthenticateAsync(user, password, cancellationToken) [optional]
  → SendAsync(message, cancellationToken)
  → DisconnectAsync(quit: true, cancellationToken)
```

Each `SendEmailAsync` call opens a new connection. For TLS behavior, see [`Core.Mailing` README — TLS Mode](../Core.Mailing/README.md#tls-mode).

## Security Notes

- **CRLF injection protection:** If `Mail.Subject` or `Mail.UnsubscribeLink` contain `\r`/`\n`, an `ArgumentException` is thrown (header injection blocking).
- **DKIM key cache:** The PEM key is parsed once via `Lazy<>` and cached; it is not re-parsed on every send.
- **Recipient validation:** At least one of To/Cc/Bcc must contain a recipient (BCC-only broadcast is supported).
