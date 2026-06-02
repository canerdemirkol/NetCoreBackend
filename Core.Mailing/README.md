# Core.Mailing

Email gönderme soyutlaması ve veri modelleri.

## Interface

```csharp
public interface IMailService
{
    void SendMail(Mail mail);
    Task SendEmailAsync(Mail mail, CancellationToken cancellationToken = default);
}
```

## Mail Modeli

```csharp
Mail mail = new()
{
    Subject = "Hoş Geldiniz!",
    TextBody = "Kaydınız tamamlandı.",
    HtmlBody = "<h1>Hoş Geldiniz!</h1><p>Kaydınız tamamlandı.</p>",
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

`MailSettings.TlsMode` (enum `MailTlsMode`) MailKit'in `SecureSocketOptions` değerine map edilir:

| Mode | SMTP davranışı | Tipik port |
|---|---|---|
| `None` | TLS yok (plaintext) | 25 (sadece dev) |
| `Auto` | MailKit port'a bakar | — |
| `SslOnConnect` | Bağlantıdan itibaren TLS | 465 |
| `StartTls` | STARTTLS zorunlu | 587 |
| `StartTlsWhenAvailable` (default) | STARTTLS varsa kullan | 587 / 25 |

Default `StartTlsWhenAvailable` çoğu modern SMTP sağlayıcı için doğrudur. Port 465 (legacy implicit-TLS) kullanıyorsan `SslOnConnect` set etmelisin.

## Implementasyon

[`Core.Mailing.MailKit`](../Core.Mailing.MailKit/README.md) — MailKit SMTP implementasyonu
