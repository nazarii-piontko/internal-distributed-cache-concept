using System.Diagnostics;
using Grpc.Net.Client;
using IDCC.Cache.Grpc;
using Microsoft.Extensions.Logging;

namespace IDCC.Cache.Peers;

internal sealed class RemotePeer(
    string id,
    uint hash,
    GrpcChannel channel,
    PeerService.PeerServiceClient client,
    ILogger<RemotePeer> logger)
    : IPeer
{
    public string Id { get; } = id;

    public uint Hash { get; } = hash;

    public PeerType Type => PeerType.Remote;
    
    public async Task<CacheValue?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new GetRequest { Key = key };
            var response = await client.GetAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response is not { HasValue: true })
                return null;
            
            var cachedValue = new CacheValue(response.Value.ToByteArray(), DateTime.UtcNow);
            return cachedValue;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get value from remote peer {Id}", Id);
            throw new InternalDistributedCacheException("Failed to get value from remote peer", ex);
        }
        finally
        {
            sw.Stop();
            logger.LogDebug("Get for {Key} took {ElapsedMilliseconds}ms", key, sw.ElapsedMilliseconds);
        }
    }

    public async Task SetAsync(string key, byte[] value, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new SetRequest
            {
                Key = key,
                Value = Google.Protobuf.ByteString.CopyFrom(value)
            };
            await client.SetAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set value on remote peer {Id}", Id);
            throw new InternalDistributedCacheException("Failed to set value on remote peer", ex);
        }
        finally
        {
            sw.Stop();
            logger.LogDebug("Set for {Key} took {ElapsedMilliseconds}ms", key, sw.ElapsedMilliseconds);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new RemoveRequest { Key = key };
            await client.RemoveAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove value from remote peer {Id}", Id);
            throw new InternalDistributedCacheException("Failed to remove value from remote peer", ex);
        }
        finally
        {
            sw.Stop();
            logger.LogDebug("Remove for {Key} took {ElapsedMilliseconds}ms", key, sw.ElapsedMilliseconds);
        }
    }

    public void Dispose()
    {
        try
        {
            channel.Dispose();
        }
        catch
        {
            // Do nothing
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}