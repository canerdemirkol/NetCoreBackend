using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.HttpProblemDetails;

public class ForbiddenProblemDetails : ProblemDetails
{
    public ForbiddenProblemDetails(string detail)
    {
        Title = "Forbidden";
        Detail = detail;
        Status = StatusCodes.Status403Forbidden;
        Type = "about:blank";
    }
}
