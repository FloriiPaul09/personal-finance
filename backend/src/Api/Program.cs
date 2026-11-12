using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.OpenApi;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Read from the final configuration when the first context is created, so test hosts can point the API at their
// own database (docs/backend/testing.md). Startup still fails right away: migrations run before serving requests.
builder.Services.AddPersistence(services =>
    services.GetRequiredService<IConfiguration>().GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required (docs/devops/configuration.md)."));

builder.Services.AddApiSecurity();
builder.Services.AddValidation();
builder.Services.AddApiDocumentation();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Pending migrations are applied before serving requests (docs/database/migrations.md).
await app.Services.MigrateDatabaseAsync();

app.UseAuthentication();
app.UseAuthorization();

// Liveness endpoint, used later by container health checks (docs/devops/docker.md).
app.MapHealthChecks("/api/health").AllowAnonymous();

app.MapApiDocumentation();
app.MapApiEndpoints();

await app.RunAsync();
