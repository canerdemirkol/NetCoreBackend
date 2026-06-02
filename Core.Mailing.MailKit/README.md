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
ConnectAsync(server, port, mapTlsMode(MailSettings.TlsMode), cancellationToken)
  → AuthenticateAsync(user, password, cancellationToken) [opsiyonel]
  → SendAsync(message, cancellationToken)
  → DisconnectAsync(quit: true, cancellationToken)
```

Her `SendEmailAsync` çağrısı yeni bir bağlantı açar. TLS davranışı için bkz. [`Core.Mailing` README — TLS Mode](../Core.Mailing/README.md#tls-mode).

## Güvenlik Notları

- **CRLF injection koruması:** `Mail.Subject` ve `Mail.UnsubscribeLink` içinde `\r`/`\n` varsa `ArgumentException` fırlatılır (header injection bloğu).
- **DKIM key cache:** PEM key `Lazy<>` ile bir kez parse edilip cache'lenir; her gönderimde tekrar parse edilmez.
- **Recipient kontrol:** To/Cc/Bcc'den en az birinde recipient olmalı (BCC-only broadcast desteklenir).
