namespace IDCC.Cache.Peers;

internal interface IPeer : IDisposable, IAsyncDisposable
{
    string Id { get; }
    
    uint Hash { get; }
    
    PeerType Type { get; }
    
    Task<GetResult> GetAsync(string key, CancellationToken cancellationToken);

    Task<SetResult> SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken);

    Task<RemoveResult> RemoveAsync(string key, long version, CancellationToken cancellationToken);
}

internal interface ILocalPeer : IPeer
{
    int CachedItemsCount { get; }
    
    IEnumerable<string> GetCachedKeys();
    
    void Remove(string key);
}

internal sealed record CacheValue(byte[] Data, long Version)
{
    public long? GetSize()
    {
        // Return approximate size of the value in bytes
        return 2 * IntPtr.Size + // object header size
               IntPtr.Size + // byte array pointer size
               Data.LongLength + // byte array size
               sizeof(long); // Version size
    }
}

internal enum GetResultStatus
{
    Found,
    NotFount,
    Failed
}

internal sealed  class GetResult
{
    public GetResultStatus Status { get; }
    
    public CacheValue? Value { get; }
    
    private GetResult(GetResultStatus status, CacheValue? value)
    {
        Status = status;
        Value = value;
    }
    
    public static GetResult Found(CacheValue value) => new(GetResultStatus.Found, value);
    
    public static GetResult NotFound() => new(GetResultStatus.NotFount, null);
    
    public static GetResult Failed() => new(GetResultStatus.Failed, null);
}    

internal enum SetResultStatus
{
    Updated,
    NewerExists,
    Failed
}

internal sealed  class SetResult
{
    public SetResultStatus Status { get; }
    
    private SetResult(SetResultStatus status)
    {
        Status = status;
    }
    
    public static SetResult Updated() => new(SetResultStatus.Updated);
    
    public static SetResult NewerExists() => new(SetResultStatus.NewerExists);
    
    public static SetResult Failed() => new(SetResultStatus.Failed);
}

internal enum RemoveResultStatus
{
    Removed,
    NotFound,
    VersionMismatch,
    Failed
}

internal sealed class RemoveResult
{
    public RemoveResultStatus Status { get; }
    
    private RemoveResult(RemoveResultStatus status)
    {
        Status = status;
    }
    
    public static RemoveResult Removed() => new(RemoveResultStatus.Removed);
    
    public static RemoveResult NotFound() => new(RemoveResultStatus.NotFound);
    
    public static RemoveResult VersionMismatch() => new(RemoveResultStatus.VersionMismatch);
    
    public static RemoveResult Failed() => new(RemoveResultStatus.Failed);
}
