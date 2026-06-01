# Core.Mailing

Email gönderme soyutlaması ve veri modelleri.

## Interface

```csharp
public interface IMailService
{
    void SendMail(Mail mail);
    Task SendEmailAsync(Mail mail);
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
    "DkimPrivateKey": "",
    "DkimSelector": "",
    "DomainName": "myapp.com"
  }
}
```

> SSL/TLS, MailKit'in default `SecureSocketOptions.Auto` davranışıyla port'a göre otomatik belirlenir (port 465 → SSL, 587 → STARTTLS). Açık konfigürasyon istersen `MailKitMailService.emailPrepare` içinde `smtp.Connect(server, port, SecureSocketOptions.X)` çağrısını override et.

## Implementasyon

[`Core.Mailing.MailKit`](../Core.Mailing.MailKit/README.md) — MailKit SMTP implementasyonu
