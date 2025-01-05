namespace IDCC.Cache.Peers;

internal interface IPeerFactory
{
    ILocalPeer CreateLocalPeer(PeerDescription peerDescription, CancellationToken cancellationToken);
    
    Task<IPeer> CreateRemotePeerAsync(PeerDescription peerDescription, CancellationToken cancellationToken);
}