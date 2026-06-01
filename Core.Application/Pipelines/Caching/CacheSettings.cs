namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Caching;

public class CacheSettings
{
    /// <summary>
    /// Default sliding expiration applied when an <c>ICachableRequest</c> does not specify its
    /// own. The value is interpreted as <b>days</b> (matches <c>TimeSpan.FromDays</c> usage in
    /// <c>CachingBehavior</c>).
    /// </summary>
    public int SlidingExpirationDays { get; set; }
}
