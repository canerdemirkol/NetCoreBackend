# Core.CrossCuttingConcerns.Exception.WebAPI

ASP.NET Core Web API için global exception handling middleware ve RFC 7807 uyumlu ProblemDetails response'ları.

## Kurulum

```csharp
// Program.cs
app.UseExceptionMiddleware(); // UseRouting'den önce
```

## HTTP Yanıt Eşleştirme

| Exception | HTTP Status | ProblemDetails Tipi |
|---|---|---|
| `BusinessException` | 400 | `BusinessProblemDetails` |
| `ValidationException` | 400 | `ValidationProblemDetails` |
| `AuthorizationException` | 401 | `AuthorizationProblemDetails` |
| `NotFoundException` | 404 | `NotFoundProblemDetails` |
| Diğer `Exception` | 500 | `InternalServerErrorProblemDetails` |

## Örnek Response

```json
// 400 - BusinessException
{
  "type": "https://example.com/probs/business",
  "title": "Business Rule Violation",
  "status": 400,
  "detail": "Bu email zaten kayıtlı."
}

// 400 - ValidationException
{
  "type": "https://example.com/probs/validation",
  "title": "Validation Error",
  "status": 400,
  "errors": {
    "Email": ["Geçerli bir email giriniz."]
  }
}
```

## Middleware Pipeline

```
UseExceptionMiddleware()   ← tüm exception'ları yakalar
UseRouting()
UseAuthentication()
UseAuthorization()
MapControllers()
```

`ExceptionMiddleware`, exception'ı loglar (`ILogger`) ve `HttpExceptionHandler` aracılığıyla response'u yazar.
