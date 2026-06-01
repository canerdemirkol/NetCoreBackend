using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.HttpProblemDetails;

// Generic 500 response. Intentionally does NOT include the exception message in the
// HTTP response — DbUpdateException / SqlException messages may leak schema or connection
// details. The real exception is logged server-side via ExceptionMiddleware.
public class InternalServerErrorProblemDetails : ProblemDetails
{
    public InternalServerErrorProblemDetails()
    {
        Title = "Internal server error";
        Detail = "An unexpected error occurred. Please contact support if the problem persists.";
        Status = StatusCodes.Status500InternalServerError;
        Type = "about:blank";
    }
}
