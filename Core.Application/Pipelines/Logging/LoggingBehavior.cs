using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction;
using NetCoreBackend.NArchitecture.Core.Security.Extensions;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Logging;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ILoggableRequest
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    public LoggingBehavior(ILogger logger, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        List<LogParameter> logParameters = [new LogParameter { Type = request.GetType().Name, Value = request }];

        Guid? tenantId = _httpContextAccessor.HttpContext?.User.GetTenantId();

        LogDetail logDetail = new()
        {
            MethodName = next.Method.Name,
            Parameters = logParameters,
            User = _httpContextAccessor.HttpContext?.User.Identity?.Name ?? "?",
            TenantId = tenantId?.ToString()
        };

        _logger.Information(JsonSerializer.Serialize(logDetail));
        return await next();
    }
}
