using System.Text;
using System.Text.Json;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NetCoreBackend.NArchitecture.Core.Security.Extensions;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Caching;

public class CacheRemovingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICacheRemoverRequest
{
    private readonly IDistributedCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CacheRemovingBehavior<TRequest, TResponse>> _logger;

    public CacheRemovingBehavior(
        IDistributedCache cache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CacheRemovingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private string BuildCacheKey(string baseKey)
    {
        Guid? tenantId = _httpContextAccessor.HttpContext?.User.GetTenantId();
        return tenantId.HasValue ? $"t:{tenantId}:{baseKey}" : baseKey;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (request.BypassCache)
            return await next(cancellationToken);

        TResponse response = await next(cancellationToken);

        if (request.CacheGroupKey != null)
            for (int i = 0; i < request.CacheGroupKey.Count(); i++)
            {
                string groupKey = BuildCacheKey(request.CacheGroupKey[i]);
                byte[]? cachedGroup = await _cache.GetAsync(groupKey, cancellationToken);
                if (cachedGroup != null)
                {
                    HashSet<string> keysInGroup = JsonSerializer.Deserialize<HashSet<string>>(
                        Encoding.UTF8.GetString(cachedGroup)
                    )!;
                    foreach (string key in keysInGroup)
                    {
                        await _cache.RemoveAsync(key, cancellationToken);
                        _logger.LogInformation($"Removed Cache -> {key}");
                    }

                    await _cache.RemoveAsync(groupKey, cancellationToken);
                    _logger.LogInformation($"Removed Cache -> {groupKey}");
                    await _cache.RemoveAsync(key: $"{groupKey}SlidingExpiration", cancellationToken);
                    _logger.LogInformation($"Removed Cache -> {groupKey}SlidingExpiration");
                }
            }

        if (request.CacheKey != null)
        {
            string cacheKey = BuildCacheKey(request.CacheKey);
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            _logger.LogInformation($"Removed Cache -> {cacheKey}");
        }

        return response;
    }
}
