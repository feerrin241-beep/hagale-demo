using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hagale.Infrastructure.Persistence;

public sealed class HagaleDbContextFactory : IDesignTimeDbContextFactory<HagaleDbContext>
{
    public HagaleDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HAGALE_MIGRATIONS_CONNECTION_STRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=HagaleDevelopment;Trusted_Connection=True;TrustServerCertificate=True;";

        return new HagaleDbContext(
            new DbContextOptionsBuilder<HagaleDbContext>()
                .UseSqlServer(connectionString)
                .Options);
    }
}
