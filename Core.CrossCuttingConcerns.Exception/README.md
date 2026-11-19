# Core.CrossCuttingConcerns.Exception

Custom exception types and an abstract exception handler infrastructure for the domain layer.

## Exception Types

| Exception | HTTP Equivalent | Usage |
|---|---|---|
| `BusinessException` | 400 Bad Request | Business rule violation |
| `ValidationException` | 400 Bad Request | FluentValidation errors |
| `AuthorizationException` | 401 Unauthorized | The caller is not known (no identity, invalid or stale token); the client refreshes and retries |
| `ForbiddenException` | 403 Forbidden | The caller is known but may not do this; derives from `AuthorizationException`; optional `Code` names the reason |
| `NotFoundException` | 404 Not Found | Record not found |

## Usage

```csharp
// Business rule violation
if (await _userRepository.AnyAsync(u => u.Email == request.Email))
    throw new BusinessException("This email is already registered.");

// Record not found
User? user = await _userRepository.GetAsync(u => u.Id == id);
if (user is null)
    throw new NotFoundException("User not found.");

// The caller is not known
string userId = _httpContextAccessor.HttpContext?.User.GetUserId()
    ?? throw new AuthorizationException("You are not authenticated.");

// The caller is known but may not do this
if (role.IsSystemRole && !isPlatformAdmin)
    throw new ForbiddenException("System roles can only be changed by the platform administrator.");
```

## ValidationException

FluentValidation errors are automatically converted into a `ValidationException`:

```csharp
// ValidationExceptionModel
{
    Property = "Email",
    Errors = ["Enter a valid email.", "Email cannot be empty."]
}
```

## ExceptionHandler (Abstract)

Can be extended when you want customized handling for different exception types:

```csharp
public class MyExceptionHandler : ExceptionHandler
{
    protected override Task HandleException(BusinessException ex) { ... }
    protected override Task HandleException(ValidationException ex) { ... }
    protected override Task HandleException(AuthorizationException ex) { ... }
    // Optional: without this override a ForbiddenException goes to the AuthorizationException handler.
    public override Task HandleException(ForbiddenException ex) { ... }
    protected override Task HandleException(NotFoundException ex) { ... }
    protected override Task HandleException(Exception ex) { ... }
}
```
