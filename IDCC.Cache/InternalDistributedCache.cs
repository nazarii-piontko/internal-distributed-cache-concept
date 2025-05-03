using IDCC.Cache.Peers;

namespace IDCC.Cache;

internal sealed class InternalDistributedCache(IPeersRegistry peersRegistry)
    : IInternalDistributedCache
{
    public async Task<CacheRetrievalResult> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var peers = peersRegistry.GetPeersForKey(key);
        var tasks = peers
            .Select(p => p.GetAsync(key, cancellationToken))
            .ToArray();

        var results = new List<PeerGetEntryResult>(tasks.Length);
        await foreach (var t in Task.WhenEach(tasks).WithCancellation(cancellationToken))
            results.Add(t.Result);

        int consensusSize = CalcConsensusSize(tasks.Length), foundCount = 0, notFoundCount = 0, failedCount = 0;
        PeerCacheEntry? entryWithMaxVersion = null;
        foreach (var result in results)
        {
            switch (result.Status)
            {
                case PeerGetEntryResultStatus.Found:
                    if (entryWithMaxVersion == null || entryWithMaxVersion.Version < result.Entry!.Version)
                        entryWithMaxVersion = result.Entry;
                    foundCount++;
                    break;
                case PeerGetEntryResultStatus.NotFound:
                    notFoundCount++;
                    break;
                case PeerGetEntryResultStatus.Failed:
                    failedCount++;
                    break;
            }
        }

        if (foundCount >= consensusSize)
            return CacheRetrievalResult.Found(entryWithMaxVersion!.Data);
        if (notFoundCount >= consensusSize)
            return CacheRetrievalResult.NotFound();
        if (failedCount >= consensusSize)
            return CacheRetrievalResult.Failed();
        return CacheRetrievalResult.Inconsistent();
    }

    public async Task<CacheUpdateStatus> SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken)
    {
        var peers = peersRegistry.GetPeersForKey(key);
        var tasks = peers
            .Select(p => p.SetAsync(key, value, version, ttlSeconds, cancellationToken))
            .ToArray();
        
        var opStatuses = new List<PeerSetEntryStatus>(tasks.Length);
        await foreach (var t in Task.WhenEach(tasks).WithCancellation(cancellationToken))
            opStatuses.Add(t.Result);
        
        int consensusSize = CalcConsensusSize(tasks.Length), newerExistsCount = 0, failedCount = 0;
        foreach (var opStatus in opStatuses)
        {
            switch (opStatus)
            {
                case PeerSetEntryStatus.NewerExists:
                    newerExistsCount++;
                    break;
                case PeerSetEntryStatus.Failed:
                    failedCount++;
                    break;
            }
        }

        if (newerExistsCount > 0)
            return CacheUpdateStatus.NewerExists;
        if (failedCount >= consensusSize)
            return CacheUpdateStatus.Failed;
        return CacheUpdateStatus.Updated;
    }

    public async Task<CacheRemoveStatus> RemoveAsync(string key, long version, CancellationToken cancellationToken = default)
    {
        var peers = peersRegistry.GetPeersForKey(key);
        var tasks = peers
            .Select(p => p.RemoveAsync(key, version, cancellationToken))
            .ToArray();
        
        var opStatuses = new List<PeerRemoveEntryStatus>(tasks.Length);
        await foreach (var t in Task.WhenEach(tasks).WithCancellation(cancellationToken))
            opStatuses.Add(t.Result);
        
        int consensusSize = CalcConsensusSize(tasks.Length), versionMismatch = 0, failedCount = 0;
        foreach (var opStatus in opStatuses)
        {
            switch (opStatus)
            {
                case PeerRemoveEntryStatus.VersionMismatch:
                    versionMismatch++;
                    break;
                case PeerRemoveEntryStatus.Failed:
                    failedCount++;
                    break;
            }
        }

        if (versionMismatch > 0)
            return CacheRemoveStatus.VersionMismatch;
        if (failedCount >= consensusSize)
            return CacheRemoveStatus.Failed;
        return CacheRemoveStatus.Removed;
    }

    public InternalDistributedCacheInfo GetInfo()
    {
        var localPeer = peersRegistry.LocalPeer;
        return new InternalDistributedCacheInfo(
            localPeer?.Id ?? string.Empty,
            peersRegistry.DiscoveredPeers.Select(p => p.Id).ToList(),
            localPeer?.CachedItemsCount ?? 0);
    }
    
    private static int CalcConsensusSize(int peers) => peers / 2 + 1;
}
