namespace IDCC.Cache.Peers;

internal interface IPeer : IDisposable, IAsyncDisposable
{
    string Id { get; }
    
    uint Hash { get; }
    
    PeerType Type { get; }
    
    Task<PeerGetEntryResult> GetAsync(string key, CancellationToken cancellationToken);

    Task<PeerSetEntryStatus> SetAsync(string key, byte[] value, long version, int? ttlSeconds, CancellationToken cancellationToken);

    Task<PeerRemoveEntryStatus> RemoveAsync(string key, long version, CancellationToken cancellationToken);
}

internal interface ILocalPeer : IPeer
{
    int CachedItemsCount { get; }
    
    IEnumerable<string> GetCachedKeys();
    
    void Remove(string key);
}

internal record PeerCacheEntry(byte[] Data, long Version)
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

internal enum PeerGetEntryResultStatus
{
    Found,
    NotFount,
    Failed
}

internal readonly struct PeerGetEntryResult
{
    public PeerGetEntryResultStatus Status { get; }
    
    public PeerCacheEntry? Entry { get; }
    
    private PeerGetEntryResult(PeerGetEntryResultStatus status, PeerCacheEntry? entry)
    {
        Status = status;
        Entry = entry;
    }
    
    public static PeerGetEntryResult Found(PeerCacheEntry entry) => new(PeerGetEntryResultStatus.Found, entry);
    
    public static PeerGetEntryResult NotFound() => new(PeerGetEntryResultStatus.NotFount, null);
    
    public static PeerGetEntryResult Failed() => new(PeerGetEntryResultStatus.Failed, null);
}    

public enum PeerSetEntryStatus
{
    Updated,
    NewerExists,
    Failed
}

internal enum PeerRemoveEntryStatus
{
    Removed,
    NotFound,
    VersionMismatch,
    Failed
}
