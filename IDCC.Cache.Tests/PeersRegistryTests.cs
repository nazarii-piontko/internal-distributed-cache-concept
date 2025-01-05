using IDCC.Cache.Hashing;
using IDCC.Cache.Peers;
using IDCC.Cache.Peers.Discovery;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IDCC.Cache.Tests;

public sealed class PeersRegistryTests
{
    private readonly IPeerFactory _peerFactory;
    private readonly IKeysDistributionHashAlgorithm _hashAlgorithm;
    private readonly PeersRegistry _registry;
    private readonly IOptions<InternalDistributedCacheOptions> _options;

    public PeersRegistryTests()
    {
        _peerFactory = Substitute.For<IPeerFactory>();
        _hashAlgorithm = Substitute.For<IKeysDistributionHashAlgorithm>();
        _options = Substitute.For<IOptions<InternalDistributedCacheOptions>>();
        
        _options.Value.Returns(new InternalDistributedCacheOptions
        {
            ReplicationFactor = 1
        });

        _registry = new PeersRegistry(
            _peerFactory,
            _hashAlgorithm,
            _options,
            NullLogger<PeersRegistry>.Instance);
    }

    [Fact]
    public async Task OnSelfDiscoveredAsync_UpdatesLocalPeer()
    {
        // Arrange
        var localPeer = ConfigureLocalPeer();
        
        // Act
        await ((IPeersDiscoveryObserver)_registry).OnSelfDiscoveredAsync(localPeer.Desc, CancellationToken.None);

        // Assert
        Assert.Same(localPeer.Peer, _registry.LocalPeer);
        Assert.Single(_registry.DiscoveredPeers);
        Assert.Contains(localPeer.Peer, _registry.DiscoveredPeers);
    }

    [Fact]
    public async Task OnPeerDiscoveredAsync_AddsNewPeersToRegistry()
    {
        // Arrange
        var localPeer = ConfigureLocalPeer();
        await ((IPeersDiscoveryObserver)_registry).OnSelfDiscoveredAsync(localPeer.Desc, CancellationToken.None);
        
        var remotePeers = ConfigureRemotePeers(2);
        
        // Act
        await ((IPeersDiscoveryObserver)_registry).OnPeerDiscoveredAsync(remotePeers.Select(p => p.Desc), CancellationToken.None);

        // Assert
        Assert.Equal(3, _registry.DiscoveredPeers.Count);
        Assert.Contains(remotePeers[0].Peer, _registry.DiscoveredPeers);
        Assert.Contains(remotePeers[1].Peer, _registry.DiscoveredPeers);
    }

    [Fact]
    public async Task OnPeerRemovedAsync_RemovesPeersFromRegistry()
    {
        // Arrange
        var localPeer = ConfigureLocalPeer();
        await ((IPeersDiscoveryObserver)_registry).OnSelfDiscoveredAsync(localPeer.Desc, CancellationToken.None);
        
        var remotePeers = ConfigureRemotePeers(2);
        await ((IPeersDiscoveryObserver)_registry).OnPeerDiscoveredAsync(remotePeers.Select(p => p.Desc), CancellationToken.None);

        // Act
        await ((IPeersDiscoveryObserver)_registry).OnPeerRemovedAsync([remotePeers[0].Desc], CancellationToken.None);

        // Assert
        Assert.Equal(2, _registry.DiscoveredPeers.Count);
        Assert.DoesNotContain(remotePeers[0].Peer, _registry.DiscoveredPeers);
        Assert.Contains(remotePeers[1].Peer, _registry.DiscoveredPeers);
        Assert.Contains(localPeer.Peer, _registry.DiscoveredPeers);

        // Verify peer was disposed
        await remotePeers[0].Peer.Received(1).DisposeAsync();
    }
    
    [Fact]
    public void GetPeersForKey_WhenNoPeers_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<InternalDistributedCacheException>(() => _registry.GetPeersForKey("test-key"));
    }

    [Fact]
    public async Task GetPeersForKey_ReturnsPeersInCorrectOrder()
    {
        // Arrange
        _options.Value.Returns(new InternalDistributedCacheOptions
        {
            ReplicationFactor = 2
        });
        
        var localPeer = ConfigureLocalPeer();
        await ((IPeersDiscoveryObserver)_registry).OnSelfDiscoveredAsync(localPeer.Desc, CancellationToken.None);
        
        var remotePeers = ConfigureRemotePeers(2);
        await ((IPeersDiscoveryObserver)_registry).OnPeerDiscoveredAsync(remotePeers.Select(p => p.Desc), CancellationToken.None);

        const string testKey = "test-key";
        const uint keyHash = 1000;

        _hashAlgorithm.ComputeKeyHash(testKey).Returns(keyHash);
        _hashAlgorithm.ComputeCombinedHash(localPeer.Peer.Hash, keyHash).Returns(5000u);
        _hashAlgorithm.ComputeCombinedHash(remotePeers[0].Peer.Hash, keyHash).Returns(3000u);
        _hashAlgorithm.ComputeCombinedHash(remotePeers[1].Peer.Hash, keyHash).Returns(4000u);
        
        // Act
        var result = _registry.GetPeersForKey(testKey).ToList();

        // Assert
        Assert.Equal(2, result.Count); // ReplicationFactor = 2
        Assert.Equal(localPeer.Peer.Id, result[0].Id); // Highest hash (5000)
        Assert.Equal(remotePeers[1].Peer.Id, result[1].Id); // Second highest hash (4000)
    }

    private List<(IPeer Peer, PeerDescription Desc)> ConfigureRemotePeers(int n)
    {
        var peers = new List<(IPeer, PeerDescription)>();

        for (var i = 1; i <= n; ++i)
        {
            var name = $"remotePeer{i}";
            var desc = new PeerDescription(name, new Uri($"http://{name}"), PeerType.Remote);
        
            var peer = Substitute.For<IPeer>();
            peer.Id.Returns(name);
            peer.Hash.Returns(1u + (uint)i);
            peer.Type.Returns(PeerType.Remote);
        
            _peerFactory.CreateRemotePeerAsync(Arg.Is<PeerDescription>(p => p.Id == name), Arg.Any<CancellationToken>()).Returns(peer);
            
            peers.Add((peer, desc));
        }

        return peers;
    }

    private (ILocalPeer Peer, PeerDescription Desc) ConfigureLocalPeer()
    {
        const string name = "localPeer";
        var desc = new PeerDescription(name, new Uri($"http://{name}"), PeerType.Local);
        
        var peer = Substitute.For<ILocalPeer>();
        peer.Id.Returns(name);
        peer.Hash.Returns(1u);
        peer.Type.Returns(PeerType.Local);
        
        _peerFactory.CreateLocalPeer(Arg.Is<PeerDescription>(p => p.Id == name), Arg.Any<CancellationToken>()).Returns(peer);

        return (peer, desc);
    }
}