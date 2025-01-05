using Grpc.Net.Client;
using IDCC.Cache.Hashing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IDCC.Cache.Peers;

internal sealed class PeerFactory(
    IKeysDistributionHashAlgorithm hashAlgorithm,
    IMemoryCache cache,
    ILogger<RemotePeer> logger)
    : IPeerFactory
{
    public ILocalPeer CreateLocalPeer(PeerDescription peerDescription, CancellationToken cancellationToken)
    {
        if (peerDescription.Type != PeerType.Local)
            throw new ArgumentException("Peer description is not for a local peer", nameof(peerDescription));
     
        var peerId = peerDescription.Id;
        var peerHash = hashAlgorithm.ComputePeerHash(peerId);
        
        return new LocalPeer(peerId, peerHash, cache);
    }

    public async Task<IPeer> CreateRemotePeerAsync(PeerDescription peerDescription, CancellationToken cancellationToken)
    {
        if (peerDescription.Type != PeerType.Remote)
            throw new ArgumentException("Peer description is not for a remote peer", nameof(peerDescription));
        
        var peerId = peerDescription.Id;
        var peerHash = hashAlgorithm.ComputePeerHash(peerId);

        var channel = GrpcChannel.ForAddress(peerDescription.Endpoint);
        await channel.ConnectAsync(cancellationToken);

        var client = new Grpc.PeerService.PeerServiceClient(channel);

        return new RemotePeer(peerId, peerHash, channel, client, logger);
    }
}