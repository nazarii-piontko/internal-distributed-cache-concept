using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client;
using IDCC.Cache.Grpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IDCC.Cache.Peers;

internal sealed class RemotePeer(
    string id,
    uint hash,
    GrpcChannel channel,
    PeerService.PeerServiceClient client,
    TimeProvider timeProvider,
    IOptions<InternalDistributedCacheOptions> options,
    ILogger<RemotePeer> logger)
    : IPeer
{
    public string Id { get; } = id;

    public uint Hash { get; } = hash;

    public PeerType Type => PeerType.Remote;
    
    public async Task<PeerGetEntryResult> GetAsync(string key, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new GetRequest { Key = key };
            var callOptions = new CallOptions(deadline: GetDeadline(), cancellationToken: cancellationToken);
            var response = await client.GetAsync(request, callOptions).ConfigureAwait(false);
            var cachedValue = new PeerCacheEntry(response.Value.ToByteArray(), response.Version);
            return PeerGetEntryResult.Found(cachedValue);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return PeerGetEntryResult.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get value from remote peer {Id}", Id);
            return PeerGetEntryResult.NotFound();
        }
        finally
        {
            sw.Stop();
            logger.LogDebug("Get for {Key} took {ElapsedMilliseconds}ms", key, sw.ElapsedMilliseconds);
        }
    }

    public async Task<PeerSetEntryStatus> SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken)
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
            
            var callOptions = new CallOptions(deadline: GetDeadline(), cancellationToken: cancellationToken);
            await client.SetAsync(request, callOptions).ConfigureAwait(false);
            
            return PeerSetEntryStatus.Updated;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted)
        {
            return PeerSetEntryStatus.NewerExists;
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

    public async Task<PeerRemoveEntryStatus> RemoveAsync(string key, long version, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new RemoveRequest
            {
                Key = key,
                Version = version
            };
            
            var callOptions = new CallOptions(deadline: GetDeadline(), cancellationToken: cancellationToken);
            await client.RemoveAsync(request, callOptions).ConfigureAwait(false);
            
            return PeerRemoveEntryStatus.Removed;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return PeerRemoveEntryStatus.NotFound;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted)
        {
            return PeerRemoveEntryStatus.VersionMismatch;
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
    
    private DateTime GetDeadline()
    {
        return timeProvider.GetUtcNow().UtcDateTime.AddMilliseconds(options.Value.PeerRequestTimeoutMs);
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