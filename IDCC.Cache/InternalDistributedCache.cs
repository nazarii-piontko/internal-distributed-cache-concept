using IDCC.Cache.Peers;
using Microsoft.Extensions.Options;

namespace IDCC.Cache;

internal sealed class InternalDistributedCache(
    IPeersRegistry peersRegistry,
    IOptions<InternalDistributedCacheOptions> options)
    : IInternalDistributedCache
{
    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var peers = peersRegistry.GetPeersForKey(key);
        var tasks = peers.Select(p => p.GetAsync(key, cancellationToken)).ToList();

        var latestCachedValue = default(CacheValue);
        var successfulCount = 0;
        await foreach (var t in Task.WhenEach(tasks).WithCancellation(cancellationToken))
        {
            if (t.IsCompletedSuccessfully)
            {
                if (t is { Result: { } cacheValue } &&
                    (latestCachedValue == null || cacheValue.CreatedAt > latestCachedValue.CreatedAt))
                    latestCachedValue = cacheValue;
                successfulCount++;
            }
        }
        
        if (successfulCount < options.Value.MinReplicationSuccesses)
            throw new AggregateException(
                "Failed to get value from peers",
                tasks.Where(t => t.IsFaulted).Select(t => t.Exception!));

        return latestCachedValue?.Value;
    }

    public async Task SetAsync(string key, byte[] value, CancellationToken cancellationToken = default)
    {
        var peers = peersRegistry.GetPeersForKey(key);
        var tasks = peers.Select(p => p.SetAsync(key, value, cancellationToken)).ToList();
        
        var successfulCount = 0;
        await foreach (var t in Task.WhenEach(tasks).WithCancellation(cancellationToken))
        {
            if (t.IsCompletedSuccessfully)
                successfulCount++;
        }
        
        if (successfulCount < options.Value.MinReplicationSuccesses)
            throw new AggregateException(
                "Failed to set value to peers",
                tasks.Where(t => t.IsFaulted).Select(t => t.Exception!));

    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var peers = peersRegistry.GetPeersForKey(key);
        var tasks = peers.Select(p => p.RemoveAsync(key, cancellationToken)).ToList();

        var successfulCount = 0;
        await foreach (var t in Task.WhenEach(tasks).WithCancellation(cancellationToken))
        {
            if (t.IsCompletedSuccessfully)
                successfulCount++;
        }
        
        if (successfulCount < options.Value.MinReplicationSuccesses)
            throw new AggregateException(
                "Failed to get value from peers",
                tasks.Where(t => t.IsFaulted).Select(t => t.Exception!));

    }

    public InternalDistributedCacheInfo GetInfo()
    {
        var localPeer = peersRegistry.LocalPeer;
        return new InternalDistributedCacheInfo(
            localPeer?.Id ?? string.Empty,
            peersRegistry.DiscoveredPeers.Select(p => p.Id).ToList(),
            localPeer?.GetCachedKeys().Count() ?? 0);
    }
}