namespace IDCC.Cache.Peers.Discovery;

internal interface IPeersDiscoveryStrategy
{
    Task StartAsync(IPeersDiscoveryObserver observer, CancellationToken cancellationToken);
    
    Task StopAsync(CancellationToken cancellationToken);
}