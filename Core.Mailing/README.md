# Core.Mailing

Email sending abstraction and data models.

## Interface

```csharp
public interface IMailService
{
    void SendMail(Mail mail);
    Task SendEmailAsync(Mail mail, CancellationToken cancellationToken = default);
}
```

## Mail Model

```csharp
Mail mail = new()
{
    Subject = "Welcome!",
    TextBody = "Your registration is complete.",
    HtmlBody = "<h1>Welcome!</h1><p>Your registration is complete.</p>",
    ToList = [new MailboxAddress("Ahmet Yılmaz", "user@example.com")],
    CcList = [],
    BccList = [],
    Attachments = [],
    UnsubscribeLink = "https://app.com/unsubscribe?token=abc"
};
```

## MailSettings

```json
{
  "MailSettings": {
    "Server": "smtp.example.com",
    "Port": 587,
    "SenderFullName": "MyApp",
    "SenderEmail": "noreply@myapp.com",
    "UserName": "smtp-user",
    "Password": "smtp-password",
    "AuthenticationRequired": true,
    "TlsMode": "StartTlsWhenAvailable",
    "DkimPrivateKey": "",
    "DkimSelector": "",
    "DomainName": "myapp.com"
  }
}
```

### TLS Mode

`MailSettings.TlsMode` (the `MailTlsMode` enum) is mapped to MailKit's `SecureSocketOptions` value:

| Mode | SMTP behavior | Typical port |
|---|---|---|
| `None` | No TLS (plaintext) | 25 (dev only) |
| `Auto` | MailKit decides based on the port | — |
| `SslOnConnect` | TLS from the moment of connection | 465 |
| `StartTls` | STARTTLS required | 587 |
| `StartTlsWhenAvailable` (default) | Use STARTTLS if available | 587 / 25 |

The default `StartTlsWhenAvailable` is correct for most modern SMTP providers. If you are using port 465 (legacy implicit TLS), you must set `SslOnConnect`.

## Implementation

[`Core.Mailing.MailKit`](../Core.Mailing.MailKit/README.md) — MailKit SMTP implementation
