using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hagale.Infrastructure.Persistence;

public sealed class HagaleDbContextFactory : IDesignTimeDbContextFactory<HagaleDbContext>
{
    public HagaleDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HAGALE_MIGRATIONS_CONNECTION_STRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=HagaleDevelopment;Trusted_Connection=True;TrustServerCertificate=True;";
        var provider = Environment.GetEnvironmentVariable("HAGALE_MIGRATIONS_PROVIDER");
        var optionsBuilder = new DbContextOptionsBuilder<HagaleDbContext>();

        if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            optionsBuilder.UseSqlServer(connectionString);
        }

        return new HagaleDbContext(optionsBuilder.Options);
    }
}
