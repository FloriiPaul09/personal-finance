using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Used by the dotnet-ef tool only (docs/database/migrations.md). The connection string defaults to the development
/// database of compose.yaml; set <c>ConnectionStrings__Default</c> to use another one.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=personal_finance;Username=personal_finance;Password=personal_finance_dev";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? DevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UsePersonalFinanceDatabase(connectionString)
            .Options;

        return new AppDbContext(options, FixedCurrentUser.None);
    }
}
