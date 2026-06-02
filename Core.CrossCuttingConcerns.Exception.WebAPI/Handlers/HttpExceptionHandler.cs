using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Handlers;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Extensions;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.HttpProblemDetails;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Handlers;

public class HttpExceptionHandler : ExceptionHandler
{
    public HttpResponse Response
    {
#pragma warning disable S112 // General or reserved exceptions should never be thrown
        get => _response ?? throw new NullReferenceException(nameof(_response));
#pragma warning restore S112 // General or reserved exceptions should never be thrown
        set => _response = value;
    }

    private HttpResponse? _response;

    public override Task HandleException(BusinessException businessException)
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        string details = new BusinessProblemDetails(businessException.Message).ToJson();
        return Response.WriteAsync(details);
    }

    public override Task HandleException(ValidationException validationException)
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        string details = new ValidationProblemDetails(validationException.Errors).ToJson();
        return Response.WriteAsync(details);
    }

    public override Task HandleException(AuthorizationException authorizationException)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        string details = new AuthorizationProblemDetails(authorizationException.Message).ToJson();
        return Response.WriteAsync(details);
    }

    public override Task HandleException(NotFoundException notFoundException)
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        string details = new NotFoundProblemDetails(notFoundException.Message).ToJson();
        return Response.WriteAsync(details);
    }

    protected override Task HandleUnknownException(System.Exception exception)
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        // Do not surface exception.Message — DB/internal errors can leak schema info.
        // Full details are logged server-side via ExceptionMiddleware.
        string details = new InternalServerErrorProblemDetails().ToJson();
        return Response.WriteAsync(details);
    }
}
