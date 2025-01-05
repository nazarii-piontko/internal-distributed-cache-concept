namespace IDCC.Cache.Peers;

internal interface IPeer : IDisposable, IAsyncDisposable
{
    string Id { get; }
    
    uint Hash { get; }
    
    PeerType Type { get; }
    
    Task<CacheValue?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, byte[] value, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

internal interface ILocalPeer : IPeer
{
    IEnumerable<string> GetCachedKeys();
}

internal sealed record CacheValue(byte[] Value, DateTime CreatedAt)
{
    public long? GetSize()
    {
        // Return approximate size of the value in bytes
        return 2 * IntPtr.Size + // object header size
               IntPtr.Size + // byte array pointer size
               Value.LongLength + // byte array size
               sizeof(long); // CreatedAt size
    }
}