namespace IDCC.Cache;

public interface IInternalDistributedCache
{
    Task<CacheRetrievalResult> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<CacheUpdateStatus> SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken = default);

    Task<CacheRemoveStatus> RemoveAsync(string key, long version, CancellationToken cancellationToken = default);
    
    InternalDistributedCacheInfo GetInfo();
}

public readonly struct CacheRetrievalResult
{
    public enum ResultStatus
    {
        Found,
        NotFound,
        Inconsistent,
        Failed
    }
    
    public ResultStatus Status { get; }
    
    public byte[]? Data { get; }

    private CacheRetrievalResult(ResultStatus status, byte[]? data = null)
    {
        Status = status;
        Data = data;
    }

    internal static CacheRetrievalResult Found(byte[] data) => new CacheRetrievalResult(ResultStatus.Found, data);
    
    internal static CacheRetrievalResult NotFound() => new CacheRetrievalResult(ResultStatus.NotFound);
    
    internal static CacheRetrievalResult Inconsistent() => new CacheRetrievalResult(ResultStatus.Inconsistent);
    
    internal static CacheRetrievalResult Failed() => new CacheRetrievalResult(ResultStatus.Failed);
}

public enum CacheUpdateStatus
{
    Updated,
    NewerExists,
    Failed,
}

public enum CacheRemoveStatus
{
    Removed,
    VersionMismatch,
    Failed,
}

public record InternalDistributedCacheInfo(string PeerId, List<string> Peers, int KeysCount)
{
}
