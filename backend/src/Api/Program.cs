using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Json;
using PersonalFinance.Api.Common.OpenApi;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Read from the final configuration when the first context is created, so test hosts can point the API at their
// own database (docs/backend/testing.md). Startup still fails right away: migrations run before serving requests.
builder.Services.AddPersistence(services =>
    services.GetRequiredService<IConfiguration>().GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required (docs/devops/configuration.md)."));

builder.Services.AddApiErrors();
builder.Services.AddApiJson();
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddValidation();
builder.Services.AddApiDocumentation();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Pending migrations are applied before serving requests (docs/database/migrations.md), then the first user is
// created if the database has none (docs/backend/security.md#initial-user).
await app.Services.MigrateDatabaseAsync();
await app.Services.EnsureInitialUserAsync();

// Before authentication, so every error — a 401 included — is a ProblemDetails with a code (docs/backend/validation-and-errors.md).
app.UseApiErrors();
app.UseApiSecurity();

// Liveness endpoint, used later by container health checks (docs/devops/docker.md).
app.MapHealthChecks("/api/health").AllowAnonymous();

app.MapApiDocumentation();
app.MapApiEndpoints();

await app.RunAsync();
