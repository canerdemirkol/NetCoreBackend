# Core.CrossCuttingConcerns.Exception.WebAPI

Global exception handling middleware and RFC 7807-compliant ProblemDetails responses for ASP.NET Core Web API.

## Installation

```csharp
// Program.cs
app.ConfigureCustomExceptionMiddleware(); // before UseRouting
```

## HTTP Response Mapping

| Exception | HTTP Status | ProblemDetails Type |
|---|---|---|
| `BusinessException` | 400 | `BusinessProblemDetails` |
| `ValidationException` | 400 | `ValidationProblemDetails` |
| `AuthorizationException` | 401 | `AuthorizationProblemDetails` |
| `NotFoundException` | 404 | `NotFoundProblemDetails` |
| Other `Exception` | 500 | `InternalServerErrorProblemDetails` |

## Example Response

```json
// 400 - BusinessException
{
  "type": "https://example.com/probs/business",
  "title": "Business Rule Violation",
  "status": 400,
  "detail": "This email is already registered."
}

// 400 - ValidationException
{
  "type": "https://example.com/probs/validation",
  "title": "Validation Error",
  "status": 400,
  "errors": {
    "Email": ["Enter a valid email."]
  }
}
```

## Middleware Pipeline

```
ConfigureCustomExceptionMiddleware()   ← catches all exceptions
UseRouting()
UseAuthentication()
UseAuthorization()
MapControllers()
```

`ExceptionMiddleware` logs the exception (`ILogger`) and writes the response through the `HttpExceptionHandler`.
