using IDCC.Cache.Hashing;
using IDCC.Cache.Peers.Discovery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IDCC.Cache.Peers;

internal sealed class PeersRegistry(
    IPeerFactory peerFactory,
    IKeysDistributionHashAlgorithm hashAlgorithm,
    IOptions<InternalDistributedCacheOptions> options,
    ILogger<PeersRegistry> logger)
    : IPeersRegistry, IPeersDiscoveryObserver
{
    private volatile IReadOnlyList<IPeer> _peers = [];

    public IReadOnlyList<IPeer> DiscoveredPeers => _peers;
    
    public ILocalPeer? LocalPeer { get; private set; }
    
    public IEnumerable<IPeer> GetPeersForKey(string key)
    {
        var peers = _peers;
        if (peers.Count == 0)
            throw new InternalDistributedCacheException("No peers available");

        var orderedPeers = from peer in peers
            let keyHash = hashAlgorithm.ComputeKeyHash(key)
            orderby hashAlgorithm.ComputeCombinedHash(peer.Hash, keyHash) descending
            select peer;
        var peersForKey = orderedPeers.Take(options.Value.ReplicationFactor);

        return peersForKey;
    }

    Task IPeersDiscoveryObserver.OnSelfDiscoveredAsync(PeerDescription self, CancellationToken cancellationToken)
    {
        var oldLocalPeer = LocalPeer;
        var newLocalPeer = peerFactory.CreateLocalPeer(self, cancellationToken);
        
        var peers = new List<IPeer>(_peers);
        if (oldLocalPeer != null)
            peers.Remove(oldLocalPeer);
        peers.Add(newLocalPeer);
        
        LocalPeer = newLocalPeer;
        _peers = peers.AsReadOnly(); // Atomic update
        
        return Task.CompletedTask;
    }
    
    async Task IPeersDiscoveryObserver.OnPeerDiscoveredAsync(IEnumerable<PeerDescription> newPeersDesc, CancellationToken cancellationToken)
    {
        var peersCreationTasks = newPeersDesc.Select(pd => peerFactory.CreateRemotePeerAsync(pd, cancellationToken));
        var newPeers = new List<IPeer>();
        await foreach (var task in Task.WhenEach(peersCreationTasks).WithCancellation(cancellationToken))
        {
            var peer = await task;
            newPeers.Add(peer);
        }

        List<IPeer> peers = [.._peers, ..newPeers];

        if (LocalPeer != null)
        {
            var replicationFactor = options.Value.ReplicationFactor;
            
            // Remove keys that should be moved to new peers
            foreach (var key in LocalPeer.GetCachedKeys())
            {
                var isKeyRemain = (from peer in peers
                        let keyHash = hashAlgorithm.ComputeKeyHash(key)
                        orderby hashAlgorithm.ComputeCombinedHash(LocalPeer.Hash, keyHash) descending
                        select peer)
                    .Take(replicationFactor)
                    .Contains(LocalPeer);

                if (isKeyRemain)
                    continue;
                
                logger.LogDebug("Removing key {Key} from {PeerId} due to migration to a new peer", key, LocalPeer.Id);
                await LocalPeer.RemoveAsync(key, cancellationToken);
            }
        }

        _peers = peers.AsReadOnly(); // Atomic update
    }

    Task IPeersDiscoveryObserver.OnPeerRemovedAsync(IEnumerable<PeerDescription> removedPeersDesc, CancellationToken cancellationToken)
    {
        var peers = new List<IPeer>(_peers);
        var peersToRemove = new List<IPeer>();
        
        foreach (var removedPeerDesc in removedPeersDesc)
        {
            var peer = peers.FirstOrDefault(p => p.Id == removedPeerDesc.Id);
            if (peer != null)
            {
                peers.Remove(peer);
                peersToRemove.Add(peer);
            }
        }

        _peers = peers.AsReadOnly(); // Atomic update
        
        return Task.WhenAll(peersToRemove.Select(p => p.DisposeAsync().AsTask()));
    }
}