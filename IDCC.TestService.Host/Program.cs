var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var postgresDb = postgres.AddDatabase("test");

builder
    .AddProject<Projects.IDCC_TestService>("test-service")
    .WithHttpHealthCheck("/health")
    .WithReference(postgresDb, "DefaultConnection")
    .WaitFor(postgresDb);

builder.Build().Run();