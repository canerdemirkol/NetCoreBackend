# Core.Security.WebApi.Swagger

Swagger UI'a JWT Bearer token desteği ekleyen Swashbuckle operation filter.

## Kurulum

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

`BearerSecurityRequirementOperationFilter`, tüm endpoint'lere otomatik olarak Bearer güvenlik gereksinimi ekler. Bu sayede Swagger UI'da kilit ikonu görünür ve token girilebilir.
