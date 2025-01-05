namespace IDCC.Cache.Peers.Discovery;

internal interface IPeersDiscoveryObserver
{
    Task OnSelfDiscoveredAsync(PeerDescription self, CancellationToken cancellationToken);
    
    Task OnPeerDiscoveredAsync(IEnumerable<PeerDescription> newPeers, CancellationToken cancellationToken);
    
    Task OnPeerRemovedAsync(IEnumerable<PeerDescription> removedPeers, CancellationToken cancellationToken);
}