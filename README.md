# Internal Distributed Cache Concept (IDCC)

A proof-of-concept implementation of a distributed in-memory cache for ASP.NET Core applications running in Kubernetes clusters. This implementation uses Murmur3-based rendezvous hashing for consistent key distribution across cache nodes.

> ⚠️ **Note**: This is a proof-of-concept implementation intended for educational purposes. While the core functionality works, it lacks many features required for production use.

## Overview

IDCC provides a simple, embeddable distributed cache that:
- Runs within your application pods
- Automatically discovers peers in the same Kubernetes namespace
- Distributes and replicates data across peers using configurable replication
- Uses gRPC for efficient peer-to-peer communication
- Stores data in-memory using `IMemoryCache`
- Requires no external infrastructure or dependencies

## How It Works

### Key Distribution with Rendezvous Hashing

The cache uses the Murmur3 algorithm to implement rendezvous hashing (also known as highest random weight hashing) for determining which peers should store each key:

```csharp
// From MurmurKeysDistributionHashAlgorithm.cs
public uint ComputeCombinedHash(uint serverHash, uint keyHash) => 
    (uint)(((ulong)serverHash * keyHash) & 0xFFFFFFFF);
```

This approach provides:
- Consistent key distribution across peers
- Minimal key redistribution when peers are added/removed
- Natural load balancing across the cluster

### Data Replication

Keys are replicated across multiple peers based on the configured `ReplicationFactor`:

```csharp
// Example configuration in appsettings.json
"InternalDistributedCache": {
  "ReplicationFactor": 3,       // Each key is stored on 3 peers
  "MinReplicationConsensusSize": 2  // Get operations need 2/3 peers to agree
}
```

### Peer Discovery in Kubernetes

IDCC automatically discovers peers in your Kubernetes cluster by:
1. Using the Kubernetes API to watch pods with matching labels
2. Tracking pod lifecycle events (creation, deletion)
3. Establishing gRPC connections between peers
4. Rebalancing keys when the peer topology changes

For non-Kubernetes environments, a `DummyPeersDiscoveryStrategy` is provided for local development.

## Getting Started

```csharp
// 1. Register the cache service
services.Configure<InternalDistributedCacheOptions>(config.GetSection("InternalDistributedCache"));
services.AddInternalDistributedCache();

// 2. Map the gRPC endpoints
app.MapInternalDistributedCache();

// 3. Configure Kestrel for HTTP and gRPC
builder.WebHost.ConfigureKestrel((context, options) => {
    options.ListenAnyIP(5000, o => o.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(5001, o => o.Protocols = HttpProtocols.Http2);
});

// 4. Use in services
public async Task<MyData?> GetDataAsync(string key)
{
    var bytes = await _cache.GetAsync(key);
    return bytes != null ? JsonSerializer.Deserialize<MyData>(bytes) : null;
}
```

## Configuration Options

```csharp
public sealed class InternalDistributedCacheOptions
{
    public const int DefaultPeerPort = 5001;
    
    // Use HTTPS for peer communication (default: false)
    public bool PeerHttps { get; set; } = false;

    // Port for peer gRPC communication
    public int PeerPort { get; set; } = DefaultPeerPort;

    // How often to check for peer changes (seconds)
    public int PeersDiscoveryIntervalSeconds { get; set; } = 8;

    // Random jitter added to discovery interval to prevent thundering herd
    public int PeersDiscoveryJitterSeconds { get; set; } = 2;
    
    // Number of peers to store each key on
    public int ReplicationFactor { get; set; } = 3;
    
    // Minimum number of peers required for consensus on Get operations
    public int MinReplicationConsensusSize { get; set; } = 2;
}
```

## Running the Test Service

The repository includes a test service that demonstrates the cache in action. It uses:
- A simple employee database with PostgreSQL
- An ASP.NET Core API that caches database results
- A k6 load testing script to evaluate cache performance

### Prerequisites

1. [Docker](https://docs.docker.com/get-docker/)
2. [Kind (Kubernetes in Docker)](https://kind.sigs.k8s.io/docs/user/quick-start/#installation)
3. [kubectl](https://kubernetes.io/docs/tasks/tools/)
4. [k6](https://k6.io/docs/get-started/installation/)

### Local Testing with Kind

1. Set up a local Kubernetes cluster and deploy the test service:
   ```bash
   cd IDCC.TestService
   make all
   ```

2. Scale the deployment to see how keys redistribute:
   ```bash
   make scale-up   # Add one replica
   make scale-down # Remove one replica
   ```

3. Run load tests to evaluate cache performance:
   ```bash
   make k6
   ```

## Architecture and Components

### Core Components

- **IInternalDistributedCache**: Main interface for cache operations
- **IPeersRegistry**: Manages the distributed peers topology
- **IKeysDistributionHashAlgorithm**: Implements rendezvous hashing
- **IPeersDiscoveryStrategy**: Discovers peers in the cluster
- **IPeer**: Represents a cache peer (local or remote)

### Data Distribution and Replication

For each cache operation:

1. The key is hashed using Murmur3
2. Peers are ranked by their combined hash weight with the key
3. The top N peers (based on ReplicationFactor) are selected
4. The operation is performed on all selected peers
5. For reads, a consensus is required based on MinReplicationConsensusSize

## Data Consistency Approach

IDCC uses a client-provided versioning approach that works perfectly for database-backed applications. The database serves as the source of truth and provides version numbers (typically timestamps or sequence IDs. Each write operation includes this version number from the source database. When conflicts occur, the higher version always wins. This solves the consistency problem without complex distributed consensus protocols.

```csharp
// Example: Caching a database entity with its version
var employee = await dbContext.Employees.FindAsync(id);
await cache.SetAsync(
    $"employee-{id}",
    JsonSerializer.SerializeToUtf8Bytes(employee),
    employee.Version, // Database-provided version
    ttlSeconds: 3600,
    cancellationToken);
```

This approach is ideal for caching scenarios where:
- You already have a database with versioned entities
- Cache entries represent database records
- The database remains the authority for data consistency

## Contributing

This is a proof-of-concept implementation intended for educational purposes. Feel free to fork, experiment, and improve upon the base concepts.

## License

[MIT](LICENSE)