using System.Text;
using System.Text.Json;
using Bogus;
using IDCC.Cache;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInternalDistributedCache();
builder.Services.AddHealthChecks();

builder.WebHost.ConfigureKestrel((ctx, options) =>
{
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
    options.ListenAnyIP(InternalDistributedCacheOptions.DefaultPeerPort, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

var app = builder.Build();

app.MapInternalDistributedCache();
app.MapHealthChecks("/health").DisableHttpMetrics();

app.UseRouting();
app.MapGet("/{id:int}", async ([FromRoute] int id,
    IInternalDistributedCache cache,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var cacheKey = id.ToString();
    var cachedValue = await cache.GetAsync(cacheKey, cancellationToken);
    if (cachedValue != null)
    {
        context.Response.Headers.Append("X-Cache", "HIT");
        return JsonSerializer.Deserialize<User>(cachedValue);
    }

    // Simulate some long operation
    await Task.Delay(1000, cancellationToken);

    var faker = new Faker<User>()
        .RuleFor(u => u.Id, f => id)
        .RuleFor(u => u.Email, f => f.Internet.Email())
        .RuleFor(u => u.FullName, f => f.Name.FullName());
    var user = faker.Generate();
    
    await cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(user), cancellationToken);
    
    context.Response.Headers.Append("X-Cache", "MISS");
    return user;
});
app.MapGet("/stat", ([FromServices] IInternalDistributedCache cache) => cache.GetInfo());

app.Run();

public sealed class User()
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
}