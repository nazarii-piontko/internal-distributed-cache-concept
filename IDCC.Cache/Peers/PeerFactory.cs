using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using IDCC.Cache.Hashing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IDCC.Cache.Peers;

internal sealed class PeerFactory : IPeerFactory
{
    private readonly IKeysDistributionHashAlgorithm _hashAlgorithm;
    private readonly IMemoryCache _cache;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<InternalDistributedCacheOptions> _options;
    private readonly MethodConfig _defaultMethodConfig;

    public PeerFactory(
        IKeysDistributionHashAlgorithm hashAlgorithm,
        IMemoryCache cache,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        IOptions<InternalDistributedCacheOptions> options)
    {
        _hashAlgorithm = hashAlgorithm;
        _cache = cache;
        _loggerFactory = loggerFactory;
        _timeProvider = timeProvider;
        _options = options;

        var config = options.Value;

        _defaultMethodConfig = new MethodConfig
        {
            Names = { MethodName.Default },
            RetryPolicy = config.PeerRequestMaxAttempts > 1
                ? new RetryPolicy
                {
                    MaxAttempts = config.PeerRequestMaxAttempts,
                    InitialBackoff = TimeSpan.FromMilliseconds(config.PeerRequestInitialBackoffMs),
                    MaxBackoff = TimeSpan.FromMilliseconds(config.PeerRequestMaxBackoffMs),
                    BackoffMultiplier = 1.5,
                    RetryableStatusCodes = { StatusCode.Unavailable }
                }
                : null
        };
    }

    public ILocalPeer CreateLocalPeer(PeerDescription peerDescription, CancellationToken cancellationToken)
    {
        if (peerDescription.Type != PeerType.Local)
            throw new ArgumentException("Peer description is not for a local peer", nameof(peerDescription));
     
        var peerId = peerDescription.Id;
        var peerHash = _hashAlgorithm.ComputePeerHash(peerId);
        
        return new LocalPeer(peerId, peerHash, _cache);
    }

    public async Task<IPeer> CreateRemotePeerAsync(PeerDescription peerDescription, CancellationToken cancellationToken)
    {
        if (peerDescription.Type != PeerType.Remote)
            throw new ArgumentException("Peer description is not for a remote peer", nameof(peerDescription));
        
        var peerId = peerDescription.Id;
        var peerHash = _hashAlgorithm.ComputePeerHash(peerId);

        var channel = GrpcChannel.ForAddress(peerDescription.Endpoint, new GrpcChannelOptions
        {
            LoggerFactory = _loggerFactory,
            ServiceConfig = new ServiceConfig { MethodConfigs = { _defaultMethodConfig } }
        });
        await channel.ConnectAsync(cancellationToken);

        var client = new Grpc.PeerService.PeerServiceClient(channel);
        var logger = _loggerFactory.CreateLogger<RemotePeer>();
        
        return new RemotePeer(peerId, peerHash, channel, client, _timeProvider, _options, logger);
    }
}