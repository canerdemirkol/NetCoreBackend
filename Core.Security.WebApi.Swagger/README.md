# Core.Security.WebApi.Swagger

A Swashbuckle operation filter that adds JWT Bearer token support to the Swagger UI.

## Installation

```csharp
// Program.cs
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    c.OperationFilter<BearerSecurityRequirementOperationFilter>();
});
```

`BearerSecurityRequirementOperationFilter` automatically adds a Bearer security requirement to all endpoints. This makes the lock icon appear in the Swagger UI and allows a token to be entered.
