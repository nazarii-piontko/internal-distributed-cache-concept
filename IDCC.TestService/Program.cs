using System.Text.Json;
using IDCC.Cache;
using InternalDistributedCache.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddDbContext<EmployeesDbContext>(o =>
{
    o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddTransient<EmployeesDbContextSeeder>();

builder.Services.Configure<InternalDistributedCacheOptions>(builder.Configuration.GetSection("InternalDistributedCache"));
builder.Services.AddInternalDistributedCache();

builder.WebHost.ConfigureKestrel((context, options) =>
{
    var httpPort = context.Configuration.GetValue("HTTP_PORTS", 5000);
    var urls = context.Configuration["URLS"];
    if (!string.IsNullOrEmpty(urls) && Uri.TryCreate(urls, UriKind.Absolute, out var uri))
        httpPort = uri.Port;
    
    options.ListenAnyIP(httpPort, listenOptions =>
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

app.UseExceptionHandler();

app.UseRouting();
app.MapGet("/{id:long}", async (
    [FromRoute] long id,
    EmployeesDbContext dbContext,
    IInternalDistributedCache cache,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var cacheKey = $"employee-{id}";
    var cachedValue = await cache.GetAsync(cacheKey, cancellationToken);
    if (cachedValue != null)
    {
        context.Response.Headers.Append("X-Cache", "HIT");
        return JsonSerializer.Deserialize<EmployeeCacheData>(cachedValue)?.Data;
    }

    var employee = await dbContext.Employers
        .Include(e => e.Department)
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    await cache.SetAsync(
        cacheKey,
        JsonSerializer.SerializeToUtf8Bytes(new EmployeeCacheData { Data = employee }),
        employee?.Version ?? 0,
        null,
        cancellationToken);

    context.Response.Headers.Append("X-Cache", "MISS");
    return employee;
});

app.MapPut("/{id:long}", async (
    [FromRoute] long id,
    [FromBody] EmployeeUpdateRequest request,
    EmployeesDbContext dbContext,
    IInternalDistributedCache cache,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var employee = await dbContext.Employers
        .Include(e => e.Department)
        .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    if (employee == null)
    {
        context.Response.StatusCode = 404;
        return null;
    }

    employee.FullName = request.FullName;
    employee.Version++;
    
    await dbContext.SaveChangesAsync(cancellationToken);

    var cacheKey = $"employee-{id}";
    await cache.SetAsync(
        cacheKey,
        JsonSerializer.SerializeToUtf8Bytes(new EmployeeCacheData { Data = employee }),
        employee.Version,
        null,
        cancellationToken);

    return employee;
});

app.MapGet("/stat", ([FromServices] IInternalDistributedCache cache) => cache.GetInfo());

app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<EmployeesDbContextSeeder>();
    seeder.Seed(1000);
}

app.Run();

public sealed class EmployeeCacheData
{
    public Employee? Data { get; init; }
}

public sealed class EmployeeUpdateRequest
{
    public string FullName { get; init; } = null!;
}