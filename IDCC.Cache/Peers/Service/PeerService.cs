using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IDCC.Cache.Grpc;
using Microsoft.Extensions.Logging;

namespace IDCC.Cache.Peers.Service;

internal sealed class PeerService(
    IPeersRegistry peersRegistry,
    ILogger<PeerService> logger)
    : Grpc.PeerService.PeerServiceBase
{
    public override async Task<GetReponse> Get(GetRequest request, ServerCallContext context)
    {
        logger.LogDebug("Get request for key: {Key}", request.Key);
        
        var response = new GetReponse();

        if (peersRegistry.LocalPeer == null)
        {
            logger.LogWarning("Local peer is not available");
            return response;
        }

        var cacheValue = await peersRegistry.LocalPeer.GetAsync(request.Key, context.CancellationToken);
        if (cacheValue != null)
        {
            response.Value = ByteString.CopyFrom(cacheValue.Value);
            response.CreatedAt = Timestamp.FromDateTime(cacheValue.CreatedAt);
        }

        return response;
    }

    public override async Task<SetReponse> Set(SetRequest request, ServerCallContext context)
    {
        logger.LogDebug("Set request for key: {Key}", request.Key);
        
        if (peersRegistry.LocalPeer != null)
            await peersRegistry.LocalPeer.SetAsync(request.Key, request.Value.ToByteArray(), context.CancellationToken);
        else
            logger.LogWarning("Local peer is not available");

        return new SetReponse();
    }

    public override async Task<RemoveResponse> Remove(RemoveRequest request, ServerCallContext context)
    {
        logger.LogDebug("Remove request for key: {Key}", request.Key);
        
        if (peersRegistry.LocalPeer != null)
            await peersRegistry.LocalPeer.RemoveAsync(request.Key, context.CancellationToken);
        else
            logger.LogWarning("Local peer is not available");

        return new RemoveResponse();
    }
}