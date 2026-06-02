using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Handlers;

public abstract class ExceptionHandler
{
    // Compile-time exhaustive dispatch. Switching on the static type means a newly added
    // exception derived from one of the known bases still routes to the right handler, and
    // anything unknown gets a defined fallback instead of vanishing into `dynamic` ambiguity.
    public Task HandleException(System.Exception exception) => exception switch
    {
        BusinessException b => HandleException(b),
        ValidationException v => HandleException(v),
        AuthorizationException a => HandleException(a),
        NotFoundException n => HandleException(n),
        _ => HandleUnknownException(exception)
    };

    public abstract Task HandleException(BusinessException businessException);
    public abstract Task HandleException(ValidationException validationException);
    public abstract Task HandleException(AuthorizationException authorizationException);
    public abstract Task HandleException(NotFoundException notFoundException);
    protected abstract Task HandleUnknownException(System.Exception exception);
}
