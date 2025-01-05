namespace IDCC.Cache.Peers;

internal interface IPeersRegistry
{
    IReadOnlyList<IPeer> DiscoveredPeers { get; }
    
    ILocalPeer? LocalPeer { get; }
    
    IEnumerable<IPeer> GetPeersForKey(string key);
}