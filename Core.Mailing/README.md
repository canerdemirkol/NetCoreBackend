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
    ToList = [new ToEmail("user@example.com", "Ahmet Yılmaz")],
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
    "UseSsl": false,
    "DkimPrivateKey": "",
    "DkimSelector": "",
    "DomainName": "myapp.com"
  }
}
```

## Implementasyon

[`Core.Mailing.MailKit`](../Core.Mailing.MailKit/README.md) — MailKit SMTP implementasyonu
