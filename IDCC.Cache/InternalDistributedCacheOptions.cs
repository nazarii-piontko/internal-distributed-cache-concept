namespace IDCC.Cache;

public sealed class InternalDistributedCacheOptions
{
    public const int DefaultPeerPort = 5001;
    
    public bool PeerHttps { get; set; } = false;

    public int PeerPort { get; set; } = DefaultPeerPort;

    public int PeersDiscoveryIntervalSeconds { get; set; } = 8;

    public int PeersDiscoveryJitterSeconds { get; set; } = 2;

    public int PeerRequestTimeoutMs { get; set; } = 60;
    
    public int PeerRequestMaxAttempts { get; set; } = 1;

    public int PeerRequestInitialBackoffMs { get; set; } = 25;
    
    public int PeerRequestMaxBackoffMs { get; set; } = 35;
    
    public int ReplicationFactor { get; set; } = 3;
}
