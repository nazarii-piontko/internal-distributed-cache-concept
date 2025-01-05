namespace IDCC.Cache.Peers;

internal sealed record PeerDescription(string Id, Uri Endpoint, PeerType Type);