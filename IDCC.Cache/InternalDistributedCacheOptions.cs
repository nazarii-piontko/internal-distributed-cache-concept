namespace IDCC.Cache;

public sealed class InternalDistributedCacheOptions
{
    public const int DefaultPeerPort = 5001;
    
    public bool PeerHttps { get; set; } = false;

    public int PeerPort { get; set; } = DefaultPeerPort;

    public int PeersDiscoveryIntervalSeconds { get; set; } = 10;

    public int PeersDiscoveryJitterSeconds { get; set; } = 2;
    
    public int ReplicationFactor { get; set; } = 3;
    
    public int MinReplicationSuccesses { get; set; } = 1;
}
