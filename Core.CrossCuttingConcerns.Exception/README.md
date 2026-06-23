# Core.CrossCuttingConcerns.Exception

Custom exception types and an abstract exception handler infrastructure for the domain layer.

## Exception Types

| Exception | HTTP Equivalent | Usage |
|---|---|---|
| `BusinessException` | 400 Bad Request | Business rule violation |
| `ValidationException` | 400 Bad Request | FluentValidation errors |
| `AuthorizationException` | 401 Unauthorized | Unauthorized access |
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

// Authorization error
if (!user.IsActive)
    throw new AuthorizationException("Your account is not active.");
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
    protected override Task HandleException(NotFoundException ex) { ... }
    protected override Task HandleException(Exception ex) { ... }
}
```
