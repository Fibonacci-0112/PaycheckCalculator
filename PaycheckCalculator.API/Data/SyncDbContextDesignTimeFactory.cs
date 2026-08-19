using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PaycheckCalculator.API.Data;

/// <summary>
/// Design-time factory used only by the <c>dotnet ef</c> tooling. Because it is present, EF builds the
/// context from here — targeting the production PostgreSQL provider — instead of executing
/// <c>Program.cs</c>, so scaffolding never triggers the app's startup database calls. Scaffolding a
/// migration needs only the model and provider (not a live connection), so the connection string just
/// has to be well-formed; it mirrors the default in <c>Program.cs</c> and can be overridden with the
/// <c>ConnectionStrings__Sync</c> environment variable.
/// </summary>
public sealed class SyncDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SyncDbContext>
{
    public SyncDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Sync")
            ?? "Host=localhost;Port=5432;Database=paycheckcalc;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<SyncDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SyncDbContext(options);
    }
}
