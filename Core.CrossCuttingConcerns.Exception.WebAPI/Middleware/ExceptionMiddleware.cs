using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Handlers;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Middleware;

public class ExceptionMiddleware
{
    // LogParameter.Value is typed as object, so unknown graphs can land here. Cycles in
    // entity models would otherwise infinitely recurse during serialization; MaxDepth caps
    // pathological nesting from ever consuming the stack.
    private static readonly JsonSerializerOptions _logJsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        MaxDepth = 32
    };

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly HttpExceptionHandler _httpExceptionHandler;
    private readonly ILogger _loggerService;
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next, IHttpContextAccessor contextAccessor, ILogger loggerService)
    {
        _next = next;
        _contextAccessor = contextAccessor;
        _loggerService = loggerService;
        _httpExceptionHandler = new HttpExceptionHandler();
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (System.Exception exception)
        {
            await LogException(context, exception);
            await HandleExceptionAsync(context.Response, exception);
        }
    }

    protected virtual Task HandleExceptionAsync(HttpResponse response, System.Exception exception)
    {
        // RFC 7807 recommends application/problem+json; explicit charset prevents UTF-8 ambiguity.
        response.ContentType = "application/problem+json; charset=utf-8";
        _httpExceptionHandler.Response = response;

        return _httpExceptionHandler.HandleException(exception);
    }

    protected virtual Task LogException(HttpContext context, System.Exception exception)
    {
        // Log structured exception data without dumping exception.ToString() (which can include
        // query strings, headers and bound parameters via inner exceptions and risks leaking
        // tokens or PII into log sinks). Stack trace is kept separately so log scrubbers can
        // strip it at the sink level if needed.
        string endpoint = $"{context.Request.Method} {context.Request.Path}";

        List<LogParameter> logParameters =
        [
            new LogParameter { Type = exception.GetType().FullName ?? "Exception", Value = exception.Message },
            new LogParameter { Type = "StackTrace", Value = exception.StackTrace ?? string.Empty }
        ];

        LogDetail logDetail =
            new()
            {
                MethodName = endpoint,
                Parameters = logParameters,
                User = _contextAccessor.HttpContext?.User.Identity?.Name ?? "?"
            };

        var logMessage = $"[GeneralLogs] {JsonSerializer.Serialize(logDetail, _logJsonOptions)}";
        _loggerService.Error(logMessage);
        return Task.CompletedTask;
    }
}
