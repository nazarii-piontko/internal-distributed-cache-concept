# Internal Distributed Cache Concept

A proof-of-concept implementation of a distributed in-memory cache for ASP.NET Core applications running in Kubernetes clusters. This implementation uses rendezvous hashing (also known as highest random weight hashing) for consistent key distribution across cache nodes.

> ⚠️ **Note**: This is a proof-of-concept implementation. While the core functionality works, it lacks many features required for production use.

## Similar Solutions

This implementation shares conceptual similarities with Hazelcast IMDG (In-Memory Data Grid), particularly in its embedded deployment mode where cache nodes run directly within application processes. While Hazelcast is implemented in Java and uses consistent hashing for data distribution, our C# implementation focuses on Kubernetes-native deployment with .NET applications and uses rendezvous hashing for simpler key distribution while maintaining similar data locality benefits.

## Features

- 🔄 Automatic peer discovery in Kubernetes clusters
- 📈 Rendezvous hashing for consistent key distribution
- 🔄 Configurable replication factor
- 🚀 gRPC-based peer communication
- 🔒 Local memory storage using `IMemoryCache`
- ⚡ No external dependencies required

## How It Works

### Key Distribution

The cache uses rendezvous hashing (HRW) to determine which peers should store each key. This provides:

- Consistent key distribution across peers
- Minimal key redistribution when peers are added/removed
- Natural load balancing

### Data Replication

Keys are replicated across multiple peers (configurable via `ReplicationFactor`):
```csharp
services.Configure<InternalDistributedCacheOptions>(options => 
{
    options.ReplicationFactor = 3; // Each key is stored on 3 peers
    options.MinReplicationSuccesses = 2; // Operations succeed if 2/3 peers respond
});
```

### Peer Discovery

The cache automatically discovers peers in your Kubernetes cluster by:
1. Watching for pod changes in the same namespace
2. Filtering pods by labels
3. Establishing gRPC connections between peers

## Usage

1. Add the cache to your services:
```csharp
services.AddInternalDistributedCache();
```

2. Map the gRPC endpoints:
```csharp
app.MapInternalDistributedCache();
```

3. Configure Kestrel to listen for gRPC:
```csharp
builder.WebHost.ConfigureKestrel((ctx, options) =>
{
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});
```

4. Use the cache in your code:
```csharp
public class MyService
{
    private readonly IInternalDistributedCache _cache;

    public MyService(IInternalDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<byte[]?> GetAsync(string key)
    {
        return await _cache.GetAsync(key);
    }
}
```

5. Configuration options

```csharp
public class InternalDistributedCacheOptions
{
    public bool PeerHttps { get; set; } = false;
    public int PeerPort { get; set; } = 5001;
    public int PeersDiscoveryIntervalSeconds { get; set; } = 10;
    public int PeersDiscoveryJitterSeconds { get; set; } = 2;
    public int ReplicationFactor { get; set; } = 3;
    public int MinReplicationSuccesses { get; set; } = 1;
}
```

## Limitations

This proof-of-concept implementation has several limitations that should be considered before using it in production environments:

### Data Management
The cache operates purely in-memory without persistence or TTL support. All data is lost when pods restart, and there's no automatic cleanup of stale data. Large cached items might consume significant memory as there's no compression support.

### Consistency and Reliability
The implementation provides only basic consistency guarantees. While it uses timestamps for conflict resolution, there's no formal consistency model implementation. During network partitions or pod failures, the cache might return stale data or fail to maintain the configured replication factor.

### Operational Aspects
The current implementation lacks several operational features necessary for production use: no metrics for monitoring cache performance, no health checks beyond basic pod liveness, no circuit breakers for handling peer failures gracefully, and no support for cache warming after pod restarts. The peer discovery mechanism, while functional, might need tuning for larger clusters.

### Performance Optimization
Batch operations are not supported, which might impact performance when dealing with multiple keys. Additionally, there's no query or pattern-based key operations, limiting its use for more complex caching scenarios.

The current C# implementation of peer selection using LINQ for ordering and filtering peers is not optimized for high-performance scenarios. Each cache operation performs sorting and peer selection using LINQ queries, which creates unnecessary object allocations and CPU overhead. A more optimized implementation would use specialized data structures to reduce the computational overhead of peer selection.

## Development and Testing

The project includes a test service that can be deployed to a local Kubernetes cluster using Kind.

### Prerequisites

1. [**Docker**](https://docs.docker.com/get-docker/)
2. [**Kind (Kubernetes in Docker)**](https://kind.sigs.k8s.io/docs/user/quick-start/#installation)
3. [**kubectl**](https://kubernetes.io/docs/tasks/tools/)
4. [**k6**](https://k6.io/docs/get-started/installation/)

### Local Testing

1. Set up the test environment with [Makefile](IDCC.TestService/Makefile):
   ```bash
   make all
   ```

2. Scale the deployment:
   ```bash
   make scale-up   # Add one replica
   make scale-down # Remove one replica
   ```

3. Run k6 load tests with web dashboard enabled:
   ```bash
   make k6
   ```

## Contributing

This is a proof-of-concept implementation. Feel free to fork, experiment, and improve. Issues and pull requests are welcome.

## License

[MIT](LICENSE)
