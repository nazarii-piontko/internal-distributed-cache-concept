using IDCC.Cache.Hashing;
using IDCC.Cache.Peers;
using IDCC.Cache.Peers.Discovery;
using IDCC.Cache.Peers.Service;
using k8s;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IDCC.Cache;

public static class InternalDistributedCacheExtensions
{
    public static IServiceCollection AddInternalDistributedCache(this IServiceCollection services)
    {
        services.AddOptions();
        services.AddGrpc();
        services.AddMemoryCache();
        
        services.AddSingleton<IInternalDistributedCache, InternalDistributedCache>();
        
        services.AddSingleton<IPeersRegistry, PeersRegistry>();
        services.AddSingleton<IPeersDiscoveryObserver>(p => (IPeersDiscoveryObserver) p.GetRequiredService<IPeersRegistry>());
        services.AddSingleton<IPeerFactory, PeerFactory>();
        
        if (KubernetesClientConfiguration.IsInCluster())
            services.AddSingleton<IPeersDiscoveryStrategy, KubernetesPeerDiscoveryStrategy>();
        else
            services.AddSingleton<IPeersDiscoveryStrategy, DummyPeersDiscoveryStrategy>();
        
        services.AddHostedService<DiscoveryStrategyHost>();
        
        services.AddSingleton<IKeysDistributionHashAlgorithm, MurmurKeysDistributionHashAlgorithm>();
        
        return services;
    }
    
    public static IApplicationBuilder MapInternalDistributedCache(this IApplicationBuilder app)
    {
        return app.MapWhen(
            ctx =>
            {
                var options = ctx.RequestServices.GetRequiredService<IOptions<InternalDistributedCacheOptions>>();
                return ctx.Connection.LocalPort == options.Value.PeerPort;
            },
            grpcAppBuilder =>
            {
                grpcAppBuilder.UseRouting();
                grpcAppBuilder.UseEndpoints(b => b.MapGrpcService<PeerService>());
                grpcAppBuilder.Run(async ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    ctx.Response.ContentType = "text/plain";
                    await ctx.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client.");
                });
            });
    }
}