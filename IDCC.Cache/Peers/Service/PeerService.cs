using Google.Protobuf;
using Grpc.Core;
using IDCC.Cache.Grpc;
using Microsoft.Extensions.Logging;

namespace IDCC.Cache.Peers.Service;

internal sealed class PeerService(
    IPeersRegistry peersRegistry,
    ILogger<PeerService> logger)
    : Grpc.PeerService.PeerServiceBase
{
    public override async Task<GetResponse> Get(GetRequest request, ServerCallContext context)
    {
        logger.LogDebug("Get request for key: {Key}", request.Key);

        CheckLocalPeer();

        var result = await peersRegistry.LocalPeer!.GetAsync(request.Key, context.CancellationToken);
        if (result.Status == PeerGetEntryResultStatus.NotFound)
            throw new RpcException(new Status(StatusCode.NotFound, "Key not found"));
        if (result.Status == PeerGetEntryResultStatus.Failed)
            throw new RpcException(new Status(StatusCode.Internal, "Failed to get value from peer"));

        var response = new GetResponse
        {
            Value = ByteString.CopyFrom(result.Entry!.Data),
            Version = result.Entry.Version
        };

        return response;
    }

    public override async Task<SetResponse> Set(SetRequest request, ServerCallContext context)
    {
        logger.LogDebug("Set request for key: {Key}", request.Key);

        CheckLocalPeer();

        var status = await peersRegistry.LocalPeer!.SetAsync(
            request.Key,
            request.Value.ToByteArray(),
            request.Version,
            request.HasTtlSeconds ? request.TtlSeconds : null,
            context.CancellationToken);

        if (status == PeerSetEntryStatus.NewerExists)
            throw new RpcException(new Status(StatusCode.Aborted, "Version mismatch, newer version is stored"));

        return new SetResponse();
    }

    public override async Task<RemoveResponse> Remove(RemoveRequest request, ServerCallContext context)
    {
        logger.LogDebug("Remove request for key: {Key}", request.Key);

        CheckLocalPeer();

        var status = await peersRegistry.LocalPeer!.RemoveAsync(
            request.Key,
            request.Version,
            context.CancellationToken);

        if (status == PeerRemoveEntryStatus.NotFound)
            throw new RpcException(new Status(StatusCode.NotFound, "Key not found"));

        if (status == PeerRemoveEntryStatus.VersionMismatch)
            throw new RpcException(new Status(StatusCode.Aborted, "Version mismatch, newer version is stored"));

        return new RemoveResponse();
    }

    private void CheckLocalPeer()
    {
        if (peersRegistry.LocalPeer != null)
            return;

        logger.LogWarning("Local peer is not available");
        throw new RpcException(new Status(StatusCode.Internal, "Local peer is not available"));
    }
}