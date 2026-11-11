using PersonalFinance.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required (docs/devops/configuration.md).");

builder.Services.AddPersistence(connectionString);
builder.Services.AddHealthChecks();

var app = builder.Build();

// Pending migrations are applied before serving requests (docs/database/migrations.md).
await app.Services.MigrateDatabaseAsync();

// Liveness endpoint, used later by container health checks (docs/devops/docker.md).
app.MapHealthChecks("/api/health");

await app.RunAsync();
