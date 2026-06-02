using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NetCoreBackend.NArchitecture.Core.Security.WebApi.Swagger.Extensions;

// One-call helper that registers the "Bearer" JWT security scheme AND wires the
// per-operation requirement filter. Consumers were previously expected to call
// AddSecurityDefinition("Bearer", ...) themselves and only got the filter from this
// package — when they forgot, Swagger UI would render a lock icon with no scheme metadata
// behind it, and "Authorize" produced no usable input.
public static class SwaggerBearerExtensions
{
    public static SwaggerGenOptions AddBearerSecurity(this SwaggerGenOptions options)
    {
        // Loud failure on duplicate registration: silently overwriting a host-supplied scheme
        // (different name/format/description) would produce confusing Swagger UI behavior
        // hours later. If a consumer wants a custom Bearer config, they shouldn't also call
        // this helper.
        if (options.SwaggerGeneratorOptions.SecuritySchemes.ContainsKey("Bearer"))
            throw new InvalidOperationException(
                "A 'Bearer' security scheme is already registered. Call AddBearerSecurity() exactly once, " +
                "or remove your AddSecurityDefinition(\"Bearer\", …) call if you want this helper to own it.");

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the JWT access token (no 'Bearer ' prefix)."
        });
        options.OperationFilter<BearerSecurityRequirementOperationFilter>();
        return options;
    }
}
