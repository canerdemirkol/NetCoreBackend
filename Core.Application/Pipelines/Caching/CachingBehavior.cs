using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetCoreBackend.NArchitecture.Core.Security.Extensions;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Caching;

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICachableRequest
{
    private readonly IDistributedCache _cache;
    private readonly CacheSettings _cacheSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        IDistributedCache cache,
        ILogger<CachingBehavior<TRequest, TResponse>> logger,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor
    )
    {
        _cache = cache;
        _logger = logger;
        _cacheSettings = configuration.GetSection("CacheSettings").Get<CacheSettings>() ?? throw new InvalidOperationException();
        _httpContextAccessor = httpContextAccessor;
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
            return await next();

        string cacheKey = BuildCacheKey(request.CacheKey);
        TResponse response;
        byte[]? cachedResponse = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            response = JsonSerializer.Deserialize<TResponse>(Encoding.Default.GetString(cachedResponse))!;
            _logger.LogInformation($"Fetched from Cache -> {cacheKey}");
        }
        else
            response = await getResponseAndAddToCache(request, cacheKey, next, cancellationToken);

        return response;
    }

    private async Task<TResponse> getResponseAndAddToCache(
        TRequest request,
        string cacheKey,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        TResponse response = await next();

        TimeSpan slidingExpiration = request.SlidingExpiration ?? TimeSpan.FromDays(_cacheSettings.SlidingExpiration);
        DistributedCacheEntryOptions cacheOptions = new() { SlidingExpiration = slidingExpiration };

        byte[] serializeData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
        await _cache.SetAsync(cacheKey, serializeData, cacheOptions, cancellationToken);
        _logger.LogInformation($"Added to Cache -> {cacheKey}");

        if (request.CacheGroupKey != null)
            await addCacheKeyToGroup(request, cacheKey, slidingExpiration, cancellationToken);

        return response;
    }

    private async Task addCacheKeyToGroup(TRequest request, string cacheKey, TimeSpan slidingExpiration, CancellationToken cancellationToken)
    {
        string groupKey = BuildCacheKey(request.CacheGroupKey!);
        byte[]? cacheGroupCache = await _cache.GetAsync(key: groupKey, cancellationToken);
        HashSet<string> cacheKeysInGroup;
        if (cacheGroupCache != null)
        {
            cacheKeysInGroup = JsonSerializer.Deserialize<HashSet<string>>(Encoding.Default.GetString(cacheGroupCache))!;
            if (!cacheKeysInGroup.Contains(cacheKey))
                cacheKeysInGroup.Add(cacheKey);
        }
        else
            cacheKeysInGroup = new HashSet<string>(new[] { cacheKey });
        byte[] newCacheGroupCache = JsonSerializer.SerializeToUtf8Bytes(cacheKeysInGroup);

        byte[]? cacheGroupCacheSlidingExpirationCache = await _cache.GetAsync(
            key: $"{groupKey}SlidingExpiration",
            cancellationToken
        );
        int? cacheGroupCacheSlidingExpirationValue = null;
        if (cacheGroupCacheSlidingExpirationCache != null)
            cacheGroupCacheSlidingExpirationValue = Convert.ToInt32(
                Encoding.Default.GetString(cacheGroupCacheSlidingExpirationCache)
            );
        if (
            cacheGroupCacheSlidingExpirationValue == null
            || slidingExpiration.TotalSeconds > cacheGroupCacheSlidingExpirationValue
        )
            cacheGroupCacheSlidingExpirationValue = Convert.ToInt32(slidingExpiration.TotalSeconds);
        byte[] serializeCachedGroupSlidingExpirationData = JsonSerializer.SerializeToUtf8Bytes(
            cacheGroupCacheSlidingExpirationValue
        );

        DistributedCacheEntryOptions cacheOptions =
            new() { SlidingExpiration = TimeSpan.FromSeconds(Convert.ToDouble(cacheGroupCacheSlidingExpirationValue)) };

        await _cache.SetAsync(key: groupKey, newCacheGroupCache, cacheOptions, cancellationToken);
        _logger.LogInformation($"Added to Cache -> {groupKey}");

        await _cache.SetAsync(
            key: $"{groupKey}SlidingExpiration",
            serializeCachedGroupSlidingExpirationData,
            cacheOptions,
            cancellationToken
        );
        _logger.LogInformation($"Added to Cache -> {groupKey}SlidingExpiration");
    }
}
