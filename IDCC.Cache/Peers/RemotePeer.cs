using System.Diagnostics;
using Grpc.Core;
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
    
    public async Task<GetResult> GetAsync(string key, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new GetRequest { Key = key };
            var response = await client.GetAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            var cachedValue = new CacheValue(response.Value.ToByteArray(), response.Version);
            return GetResult.Found(cachedValue);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return GetResult.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get value from remote peer {Id}", Id);
            return GetResult.NotFound();
        }
        finally
        {
            sw.Stop();
            logger.LogDebug("Get for {Key} took {ElapsedMilliseconds}ms", key, sw.ElapsedMilliseconds);
        }
    }

    public async Task<SetResult> SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new SetRequest
            {
                Key = key,
                Value = Google.Protobuf.ByteString.CopyFrom(value),
                Version = version,
            };
            if (ttlSeconds.HasValue)
                request.TtlSeconds = ttlSeconds.Value;
            
            await client.SetAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            return SetResult.Updated();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted)
        {
            return SetResult.NewerExists();
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

    public async Task<RemoveResult> RemoveAsync(string key, long version, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new RemoveRequest
            {
                Key = key,
                Version = version
            };
            
            await client.RemoveAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            return RemoveResult.Removed();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return RemoveResult.NotFound();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted)
        {
            return RemoveResult.VersionMismatch();
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