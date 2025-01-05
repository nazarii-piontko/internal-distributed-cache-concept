using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IDCC.Cache.Peers.Discovery;

internal sealed class DummyPeersDiscoveryStrategy(
    IOptions<InternalDistributedCacheOptions> options,
    ILogger<DummyPeersDiscoveryStrategy> logger)
    : IPeersDiscoveryStrategy
{
    public Task StartAsync(IPeersDiscoveryObserver observer, CancellationToken cancellationToken)
    {
        logger.LogInformation("Start dummy peers discovery");
        
        Task.Run(() =>
        {
            var peerDescription = new PeerDescription("dummy", new Uri($"http://localhost:{options.Value.PeerPort}"), PeerType.Local);
            observer.OnSelfDiscoveredAsync(peerDescription,CancellationToken.None);
        }, cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stop dummy peers discovery");
        return Task.CompletedTask;
    }
}