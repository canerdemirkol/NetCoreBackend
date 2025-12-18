using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NetCoreBackend.NArchitecture.Core.Security.WebApi.Swagger.Extensions;

public class BearerSecurityRequirementOperationFilter : IOperationFilter
{
    private const string SecuritySchemeName = "Bearer";
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var securitySchemeReference = new OpenApiSecuritySchemeReference(SecuritySchemeName);

        var securityRequirement = new OpenApiSecurityRequirement
        {
            { securitySchemeReference, new List<string>() }
        };

        operation.Security ??= [];
        operation.Security.Add(securityRequirement);
    }
}
