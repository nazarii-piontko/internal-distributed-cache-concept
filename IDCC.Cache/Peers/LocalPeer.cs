using Microsoft.Extensions.Caching.Memory;

namespace IDCC.Cache.Peers;

internal sealed class LocalPeer : ILocalPeer
{
    private readonly IMemoryCache _cache;

    public LocalPeer(string id, uint hash, IMemoryCache cache)
    {
        Id = id;
        Hash = hash;

        _cache = cache;
    }

    public string Id { get; }
    
    public uint Hash { get; }
    
    public PeerType Type => PeerType.Local;
    
    public Task<CacheValue?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var cacheKey = new CacheKey(key);
        var cacheValue = _cache.Get<CacheValue>(cacheKey);
        return Task.FromResult(cacheValue);
    }

    public Task SetAsync(string key, byte[] value, CancellationToken cancellationToken)
    {
        var cachedKey = new CacheKey(key);
        var cachedValue = new CacheValue(value, DateTime.UtcNow);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.Normal,
            Size = cachedValue.GetSize(),
        };
        
        _cache.Set(cachedKey, cachedValue, cacheOptions);
        
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var cacheKey = new CacheKey(key);
        _cache.Remove(cacheKey);
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetCachedKeys()
    {
        if (_cache is MemoryCache memoryCache)
            return memoryCache.Keys.OfType<CacheKey>().Select(k => k.Key);
        return [];
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    public ValueTask DisposeAsync()
    {
        // Nothing to dispose
        return ValueTask.CompletedTask;
    }
    
    private sealed record CacheKey(string Key);
}