using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventPulse.IdentityService.Data;

/// <summary>
/// Design-time factory used by dotnet-ef tooling (migrations, scaffolding).
/// Not used at application runtime.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Connection string is only needed to satisfy design-time tooling.
        // The actual runtime connection string is in appsettings.json.
        const string designTimeConnectionString =
            "Host=localhost;Port=5433;Database=eventpulse_identity;Username=postgres;Password=postgres_dev_password";

        optionsBuilder.UseNpgsql(designTimeConnectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
