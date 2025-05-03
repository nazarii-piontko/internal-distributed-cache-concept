namespace IDCC.Cache;

public interface IInternalDistributedCache
{
    Task<CacheRetrivalResult> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<CacheUpdateStatus> SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken = default);

    Task<CacheRemoveStatus> RemoveAsync(string key, long version, CancellationToken cancellationToken = default);
    
    InternalDistributedCacheInfo GetInfo();
}

public readonly struct CacheRetrivalResult
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

    private CacheRetrivalResult(ResultStatus status, byte[]? data = null)
    {
        Status = status;
        Data = data;
    }

    internal static CacheRetrivalResult Found(byte[] data) => new CacheRetrivalResult(ResultStatus.Found, data);
    
    internal static CacheRetrivalResult NotFound() => new CacheRetrivalResult(ResultStatus.NotFound);
    
    internal static CacheRetrivalResult Inconsistent() => new CacheRetrivalResult(ResultStatus.Inconsistent);
    
    internal static CacheRetrivalResult Failed() => new CacheRetrivalResult(ResultStatus.Failed);
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
