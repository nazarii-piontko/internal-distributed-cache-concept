namespace IDCC.Cache;

public sealed class InternalDistributedCacheException : Exception
{
    public InternalDistributedCacheException(string message)
        : base(message)
    {
    }
    
    public InternalDistributedCacheException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}