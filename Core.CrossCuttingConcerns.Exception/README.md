# Core.CrossCuttingConcerns.Exception

Domain katmanı için özel exception tipleri ve soyut exception handler altyapısı.

## Exception Tipleri

| Exception | HTTP Karşılığı | Kullanım |
|---|---|---|
| `BusinessException` | 400 Bad Request | İş kuralı ihlali |
| `ValidationException` | 400 Bad Request | FluentValidation hataları |
| `AuthorizationException` | 401 Unauthorized | Yetkisiz erişim |
| `NotFoundException` | 404 Not Found | Kayıt bulunamadı |

## Kullanım

```csharp
// Business rule ihlali
if (await _userRepository.AnyAsync(u => u.Email == request.Email))
    throw new BusinessException("Bu email zaten kayıtlı.");

// Kayıt bulunamadı
User? user = await _userRepository.GetAsync(u => u.Id == id);
if (user is null)
    throw new NotFoundException("Kullanıcı bulunamadı.");

// Yetki hatası
if (!user.IsActive)
    throw new AuthorizationException("Hesabınız aktif değil.");
```

## ValidationException

FluentValidation hataları otomatik olarak `ValidationException`'a dönüştürülür:

```csharp
// ValidationExceptionModel
{
    Property = "Email",
    Errors = ["Geçerli bir email giriniz.", "Email boş olamaz."]
}
```

## ExceptionHandler (Soyut)

Farklı exception tiplerine göre özelleştirilmiş işlem yapılmak istendiğinde genişletilebilir:

```csharp
public class MyExceptionHandler : ExceptionHandler
{
    protected override Task HandleException(BusinessException ex) { ... }
    protected override Task HandleException(ValidationException ex) { ... }
    protected override Task HandleException(AuthorizationException ex) { ... }
    protected override Task HandleException(NotFoundException ex) { ... }
    protected override Task HandleException(Exception ex) { ... }
}
```
