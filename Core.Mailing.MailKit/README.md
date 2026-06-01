# Core.Mailing.MailKit

`IMailService`'in MailKit tabanlı SMTP implementasyonu. DKIM imzalama desteği içerir.

## Kurulum

```csharp
// Program.cs
builder.Services.Configure<MailSettings>(config.GetSection("MailSettings"));
builder.Services.AddTransient<IMailService, MailKitMailService>();
```

## DKIM İmzalama

DKIM, gönderilen email'lerin gerçekten domain'den geldiğini doğrular. Spam filtrelerine takılmayı önler.

```json
{
  "MailSettings": {
    "DkimPrivateKey": "-----BEGIN RSA PRIVATE KEY-----\n...",
    "DkimSelector": "mail",
    "DomainName": "myapp.com"
  }
}
```

DKIM bilgileri boş bırakılırsa imzalama atlanır.

## SMTP Bağlantı Akışı

```
ConnectAsync(server, port)
  → AuthenticateAsync(user, password) [opsiyonel]
  → SendAsync(message)
  → DisconnectAsync()
```

Her `SendEmailAsync` çağrısı yeni bir bağlantı açar.
