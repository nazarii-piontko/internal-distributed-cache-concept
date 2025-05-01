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
        var tasks = peers
            .Select(p => p.GetAsync(key, cancellationToken))
            .ToList();

        var results = new List<GetResult>(tasks.Count);
        await foreach (var t in Task.WhenEach(tasks).WithCancellation(cancellationToken))
        {
            // Null in case of exception is not ideal, but it is fine for test purposes
            results.Add(t.Result);
        }

        var maxVersionResult = results.Where(x => x.Status == GetResultStatus.Found).MaxBy(x => x.Value!.Version);
        var maxVersion = maxVersionResult?.Value!.Version ?? long.MinValue;
        var maxVersionCount = results.Where(x => x.Status == GetResultStatus.Found).Count(x => x.Value!.Version == maxVersion);
        var notFoundCount = results.Count(x => x.Status == GetResultStatus.NotFount);

        if (maxVersionCount > notFoundCount && maxVersionCount >= options.Value.MinReplicationConsensusSize)
            return maxVersionResult?.Value!.Data;
        return null;
    }

    public Task SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken)
    {
        var peers = peersRegistry.GetPeersForKey(key);
        var tasks = peers.Select(p => p.SetAsync(key, value, version, ttlSeconds, cancellationToken));
        
        return Task.WhenAll(tasks);
    }

    public Task RemoveAsync(string key, long version, CancellationToken cancellationToken = default)
    {
        var peers = peersRegistry.GetPeersForKey(key);
        var tasks = peers.Select(p => p.RemoveAsync(key, version, cancellationToken));

        return Task.WhenAll(tasks);
    }

    public InternalDistributedCacheInfo GetInfo()
    {
        var localPeer = peersRegistry.LocalPeer;
        return new InternalDistributedCacheInfo(
            localPeer?.Id ?? string.Empty,
            peersRegistry.DiscoveredPeers.Select(p => p.Id).ToList(),
            localPeer?.CachedItemsCount ?? 0);
    }
}
