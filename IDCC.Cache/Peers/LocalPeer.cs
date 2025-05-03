using Microsoft.Extensions.Caching.Memory;

namespace IDCC.Cache.Peers;

internal sealed class LocalPeer : ILocalPeer
{
    private readonly IMemoryCache _cache;
    private readonly Lock _lock = new();

    public LocalPeer(string id, uint hash, IMemoryCache cache)
    {
        Id = id;
        Hash = hash;

        _cache = cache;
    }

    public string Id { get; }
    
    public uint Hash { get; }
    
    public PeerType Type => PeerType.Local;
    
    public int CachedItemsCount => _cache is MemoryCache memoryCache ? memoryCache.Count : 0;
    
    public Task<PeerGetEntryResult> GetAsync(string key, CancellationToken cancellationToken)
    {
        var cacheKey = new CacheKey(key);
        
        // Do not use lock here, as this is a read operation and IMemoryCache is thread-safe
        // ReSharper disable once InconsistentlySynchronizedField
        var cacheValue = _cache.Get<PeerCacheEntry>(cacheKey);
        
        return Task.FromResult(cacheValue == null ? PeerGetEntryResult.NotFound() : PeerGetEntryResult.Found(cacheValue));
    }

    public Task<PeerSetEntryStatus> SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken)
    {
        var cachedKey = new CacheKey(key);
        var cachedValue = new PeerCacheEntry(value, version);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.Normal,
            Size = cachedValue.GetSize(),
            AbsoluteExpirationRelativeToNow = ttlSeconds.HasValue
                ? TimeSpan.FromSeconds(ttlSeconds.Value)
                : null,
        };

        // Use lock to ensure Compare-and-Swap behavior
        // This lock implementation is not optimal for production use, but is sufficient for this example
        lock (_lock)
        {
            var existingValue = _cache.Get<PeerCacheEntry>(cachedKey);
            if (existingValue == null || existingValue.Version < version)
            {
                _cache.Set(cachedKey, cachedValue, cacheOptions);
                return Task.FromResult(PeerSetEntryStatus.Updated); 
            }

            // Newer version exists, do not update
            return Task.FromResult(PeerSetEntryStatus.NewerExists);
        }
    }

    public Task<PeerRemoveEntryStatus> RemoveAsync(string key, long version, CancellationToken cancellationToken)
    {
        var cacheKey = new CacheKey(key);

        // Use lock to ensure Compare-and-Swap behavior
        // This lock implementation is not optimal for production use, but is sufficient for this example
        lock (_lock)
        {
            var existingValue = _cache.Get<PeerCacheEntry>(cacheKey);
            if (existingValue is null)
            {
                return Task.FromResult(PeerRemoveEntryStatus.NotFound);
            }
            
            if (existingValue.Version != version)
            {
                return Task.FromResult(PeerRemoveEntryStatus.VersionMismatch);
            }

            _cache.Remove(cacheKey);
            
            return Task.FromResult(PeerRemoveEntryStatus.Removed);
        }
    }

    public IEnumerable<string> GetCachedKeys()
    {
        // Do not use lock here, as this is a read operation and IMemoryCache is thread-safe
        // ReSharper disable once InconsistentlySynchronizedField
        if (_cache is MemoryCache memoryCache)
            return memoryCache.Keys.OfType<CacheKey>().Select(k => k.Key);
        return [];
    }

    public void Remove(string key)
    {
        var cacheKey = new CacheKey(key);
        _cache.Remove(cacheKey);
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