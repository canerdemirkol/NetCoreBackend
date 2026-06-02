using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NetCoreBackend.NArchitecture.Core.Security.WebApi.Swagger.Extensions;

public class BearerSecurityRequirementOperationFilter : IOperationFilter
{
    private const string SecuritySchemeName = "Bearer";
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Skip [AllowAnonymous] endpoints — otherwise Swagger UI shows a lock icon and prompts
        // for a token on public actions like /health or /auth/login, which is misleading and
        // breaks the "try it out" flow for unauthenticated calls.
        bool isAnonymous = context.MethodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length != 0
            || (context.MethodInfo.DeclaringType?.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length ?? 0) != 0;
        if (isAnonymous)
            return;

        var securitySchemeReference = new OpenApiSecuritySchemeReference(SecuritySchemeName);

        var securityRequirement = new OpenApiSecurityRequirement
        {
            { securitySchemeReference, new List<string>() }
        };

        operation.Security ??= [];
        operation.Security.Add(securityRequirement);
    }
}
