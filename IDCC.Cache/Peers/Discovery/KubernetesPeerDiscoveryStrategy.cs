using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IDCC.Cache.Peers.Discovery;

internal sealed class KubernetesPeerDiscoveryStrategy : IPeersDiscoveryStrategy, IDisposable
{
    private readonly IOptions<InternalDistributedCacheOptions> _options;
    private readonly ILogger<KubernetesPeerDiscoveryStrategy> _logger;
    
    private readonly KubernetesClientConfiguration _k8sConfig;
    private readonly IKubernetes _k8s;
    
    private IPeersDiscoveryObserver _observer = null!;
    
    private string _labelSelector = null!;
    
    private readonly CancellationTokenSource _stopTokenSource = new();
    private Task _watchPeersTask = null!;
    
    private readonly Dictionary<string, PeerDescription> _knownPeers = new();

    public KubernetesPeerDiscoveryStrategy(
        IOptions<InternalDistributedCacheOptions> options,
        ILogger<KubernetesPeerDiscoveryStrategy> logger)
    {
        _options = options;
        _logger = logger;
        _k8sConfig = KubernetesClientConfiguration.InClusterConfig();
        _k8s = new Kubernetes(_k8sConfig);
    }
    
    public async Task StartAsync(IPeersDiscoveryObserver observer, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Start Kubernetes peers discovery");
        
        _observer = observer;
        
        var selfPod = await GetSelfPodAsync(cancellationToken);
        var selfDescription = CreatePeerDescription(selfPod, PeerType.Local);
        
        _labelSelector = string.Join(",", selfPod.Metadata.Labels.Select(l => $"{l.Key}={l.Value}"));
        
        _knownPeers.Clear();
        _knownPeers.Add(selfPod.Metadata.Uid, selfDescription);
        
        _logger.LogInformation("Self discover: {@SelfDescription}", selfDescription);
        await observer.OnSelfDiscoveredAsync(selfDescription, cancellationToken);

        _stopTokenSource.TryReset();
        _watchPeersTask = WatchPeersAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stop Kubernetes peers discovery");
        
        try
        {
            await _stopTokenSource.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            await _watchPeersTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
    
    private async Task WatchPeersAsync()
    {
        try
        {
            var options = _options.Value;
            var cancellationToken = _stopTokenSource.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var podList = await _k8s.CoreV1.ListNamespacedPodAsync(
                        _k8sConfig.Namespace,
                        labelSelector: _labelSelector,
                        cancellationToken: cancellationToken);
                    var podByUid = podList.Items.ToDictionary(p => p.Metadata.Uid);
                    
                    var newPeers = new List<PeerDescription>();
                    foreach (var pod in podList.Items)
                    {
                        // Skip pods that aren't running
                        if (pod.Status.Phase != "Running" || 
                            pod.Status.PodIP == null || 
                            pod.Metadata.Name == Environment.MachineName)
                        {
                            continue;
                        }

                        // Skip known pods
                        if (_knownPeers.ContainsKey(pod.Metadata.Uid))
                            continue;
                        
                        var newPeerDesc = CreatePeerDescription(pod, PeerType.Remote);
                        _knownPeers.Add(pod.Metadata.Uid, newPeerDesc);
                        newPeers.Add(newPeerDesc);
                    }
                    
                    var removedPeers = _knownPeers
                        .Where(kvp => kvp.Value.Type == PeerType.Remote)
                        .Where(kvp => !podByUid.ContainsKey(kvp.Key))
                        .Select(kvp => kvp.Value)
                        .ToList();

                    foreach (var removedPeer in removedPeers)
                        _knownPeers.Remove(removedPeer.Id);
                    
                    if (removedPeers.Count > 0)
                    {
                        _logger.LogInformation("Detected {Count} removed peers: {@RemovedPeers}", removedPeers.Count, removedPeers);
                        await _observer.OnPeerRemovedAsync(removedPeers, cancellationToken);
                    }

                    if (newPeers.Count > 0)
                    {
                        _logger.LogInformation("Detected {Count} new peers: {@NewPeers}", newPeers.Count, newPeers);
                        await _observer.OnPeerDiscoveredAsync(newPeers, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error watching Kubernetes peers");
                }

                var delaySeconds = options.PeersDiscoveryIntervalSeconds +
                                   Random.Shared.Next(-options.PeersDiscoveryJitterSeconds, options.PeersDiscoveryJitterSeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, just exit
        }
    }
    
    private async Task<V1Pod> GetSelfPodAsync(CancellationToken cancellationToken)
    {
        var pod = await _k8s.CoreV1.ReadNamespacedPodAsync(
            Environment.MachineName,
            _k8sConfig.Namespace,
            cancellationToken: cancellationToken);

        return pod;
    }
    
    private PeerDescription CreatePeerDescription(V1Pod pod, PeerType peerType)
    {
        var options = _options.Value;
        var uriBuilder = new UriBuilder
        {
            Scheme = options.PeerHttps ? "https" : "http",
            Host = pod.Status.PodIP,
            Port = options.PeerPort
        };
        return new PeerDescription(pod.Metadata.Uid, uriBuilder.Uri, peerType);
    }

    public void Dispose()
    {
        _k8s.Dispose();
        _stopTokenSource.Dispose();
    }
}