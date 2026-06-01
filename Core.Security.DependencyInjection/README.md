# Core.Security.DependencyInjection

`Core.Security` servislerini DI container'a kaydeden extension metot.

## Kurulum

```csharp
// Program.cs
builder.Services.AddSecurityServices();
```

Kaydedilen servisler:

| Servis | Yaşam Döngüsü |
|---|---|
| `JwtHelper` | Scoped |
| `EmailAuthenticatorHelper` | Scoped |
| `OtpAuthenticatorHelper` | Scoped |
