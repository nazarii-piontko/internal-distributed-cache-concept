using IDCC.Cache.Peers;
using Microsoft.Extensions.Caching.Memory;

namespace IDCC.Cache.Tests;

public sealed class LocalPeerTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly LocalPeer _peer;
    
    public LocalPeerTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _peer = new LocalPeer("local-peer", 1u, _cache);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        Assert.Equal("local-peer", _peer.Id);
        Assert.Equal(1u, _peer.Hash);
        Assert.Equal(PeerType.Local, _peer.Type);
    }
    
    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _peer.GetAsync("non-existent-key", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WhenKeyExists_ReturnsCachedValue()
    {
        // Arrange
        const string key = "test-key";
        await _peer.SetAsync(key, [1], CancellationToken.None);
        
        // Act
        var result = await _peer.GetAsync(key, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal([1], result.Value);
    }

    [Fact]
    public async Task RemoveAsync_RemovesValueFromCache()
    {
        // Arrange
        const string key = "test-key";
        await _peer.SetAsync(key, [1], CancellationToken.None);

        // Act
        await _peer.RemoveAsync(key, CancellationToken.None);

        // Assert
        var result = await _peer.GetAsync(key, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCachedKeys_ReturnsAllKeys()
    {
        // Arrange
        await _peer.SetAsync("key1", [1], CancellationToken.None);
        await _peer.SetAsync("key2", [2], CancellationToken.None);
        await _peer.SetAsync("key3", [3], CancellationToken.None);

        // Act
        var keys = _peer.GetCachedKeys().ToList();

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Contains("key1", keys);
        Assert.Contains("key2", keys);
        Assert.Contains("key3", keys);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}