using System.Text;
using System.Text.Json;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
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
        _cacheSettings = configuration.GetSection("CacheSettings").Get<CacheSettings>()
            ?? throw new InvalidOperationException("CacheSettings section is missing from configuration.");
        if (_cacheSettings.SlidingExpirationDays <= 0)
            throw new InvalidOperationException(
                $"CacheSettings.SlidingExpirationDays must be a positive number of days (was {_cacheSettings.SlidingExpirationDays}).");
        _httpContextAccessor = httpContextAccessor;
    }

    // Both CacheKey and CacheGroupKey are namespaced ONLY by tenant — not by request type.
    // Reason: CacheRemovingBehavior runs on commands (e.g. CreateProductCommand) and must be
    // able to invalidate keys/groups written by queries (e.g. GetProductsQuery). If queries
    // prefixed keys with their type, commands could no longer target them.
    //
    // CONSEQUENCE for consuming apps: CacheKey strings MUST be unique across query handlers
    // (e.g. use "Products:GetAll", "Products:ById:{id}", never bare "Products").
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

        string cacheKey = BuildCacheKey(request.CacheKey);
        TResponse response;
        byte[]? cachedResponse = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            // A null deserialization result means the cached payload is incompatible with the
            // current TResponse contract (schema change, type rename, etc.). Treat it as a miss
            // and overwrite, rather than returning a silent null to the caller.
            TResponse? deserialized = JsonSerializer.Deserialize<TResponse>(Encoding.UTF8.GetString(cachedResponse));
            if (deserialized is not null)
            {
                _logger.LogInformation($"Fetched from Cache -> {cacheKey}");
                return deserialized;
            }

            _logger.LogWarning("Cached value at {CacheKey} could not be deserialized into {ResponseType}; evicting.", cacheKey, typeof(TResponse).Name);
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }

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
        TResponse response = await next(cancellationToken);

        TimeSpan slidingExpiration = request.SlidingExpiration ?? TimeSpan.FromDays(_cacheSettings.SlidingExpirationDays);
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
            cacheKeysInGroup = JsonSerializer.Deserialize<HashSet<string>>(Encoding.UTF8.GetString(cacheGroupCache))!;
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
                Encoding.UTF8.GetString(cacheGroupCacheSlidingExpirationCache)
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
