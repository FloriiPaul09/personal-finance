# syntax=docker/dockerfile:1
# Image of the API — docs/devops/docker.md, ADR-0032.
# Build from the repository root: docker build -t personal-finance-api backend
#
# Both stages are pinned to the patch release of .NET the backend packages use (10.0.15 / SDK 10.0.404);
# Dependabot updates them. A new .NET major is one planned change with its ADR (ADR-0031).

# --- Build -----------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble AS build
WORKDIR /src

# Restore first, from the project files only, so the layer is reused while dependencies do not change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Api/PersonalFinance.Api.csproj src/Api/
COPY src/Domain/PersonalFinance.Domain.csproj src/Domain/
COPY src/Infrastructure/PersonalFinance.Infrastructure.csproj src/Infrastructure/
RUN dotnet restore src/Api/PersonalFinance.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/PersonalFinance.Api.csproj \
    --configuration Release --no-restore --output /app \
    -p:UseAppHost=false

# --- Runtime ---------------------------------------------------------------------------------------
# Ubuntu chiseled ("distroless"): no shell, no package manager, a non-root user (app, UID 1654).
# "extra" adds ICU and time zone data, so text behaves as in the tests (no invariant globalization).
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app .

# Already the defaults of the image, written here so the contract is visible:
# HTTP on 8080 (nginx in front of it), Production, non-root user.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
USER app
EXPOSE 8080

# Migrations run at startup (ADR-0010): the database must be reachable before the API starts.
ENTRYPOINT ["dotnet", "PersonalFinance.Api.dll"]
