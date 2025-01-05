namespace IDCC.Cache.Hashing;

internal interface IKeysDistributionHashAlgorithm
{
    uint ComputePeerHash(string peerId);
    
    uint ComputeKeyHash(string key);
    
    uint ComputeCombinedHash(uint peerHash, uint keyHash);
}