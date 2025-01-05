using Microsoft.Extensions.Hosting;

namespace IDCC.Cache.Peers.Discovery;

internal sealed class DiscoveryStrategyHost : IHostedService
{
    private readonly IPeersDiscoveryStrategy _discoveryStrategy;
    private readonly IPeersDiscoveryObserver _observer;

    public DiscoveryStrategyHost(IPeersDiscoveryStrategy discoveryStrategy, IPeersDiscoveryObserver observer)
    {
        _discoveryStrategy = discoveryStrategy;
        _observer = observer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _discoveryStrategy.StartAsync(_observer, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _discoveryStrategy.StopAsync(cancellationToken);
    }
}