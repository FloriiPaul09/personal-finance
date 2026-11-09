var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

// Liveness endpoint, used later by container health checks (docs/devops/docker.md).
app.MapHealthChecks("/api/health");

app.Run();
