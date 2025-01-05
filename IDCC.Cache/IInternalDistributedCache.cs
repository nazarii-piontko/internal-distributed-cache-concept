namespace IDCC.Cache;

public interface IInternalDistributedCache
{
    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, byte[] value, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    InternalDistributedCacheInfo GetInfo();
}

public record InternalDistributedCacheInfo(string PeerId, List<string> Peers, int KeysCount)
{
}