namespace IDCC.Cache.Hashing;

internal sealed class MurmurKeysDistributionHashAlgorithm : IKeysDistributionHashAlgorithm
{
    private const uint PeerHashSeed = 1818163;
    private const uint KeyHashSeed = 6722263;

    public uint ComputePeerHash(string peerId) => Murmur3.Compute(peerId.AsSpan(), PeerHashSeed);
    
    public uint ComputeKeyHash(string key) => Murmur3.Compute(key.AsSpan(), KeyHashSeed);
    
    public uint ComputeCombinedHash(uint serverHash, uint keyHash) => (uint)(((ulong)serverHash * keyHash) & 0xFFFFFFFF);
}