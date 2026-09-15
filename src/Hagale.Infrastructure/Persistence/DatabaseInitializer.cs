using Hagale.Application.Authentication;
using Hagale.Domain.Pricing;
using Hagale.Domain.Rides;
using Hagale.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hagale.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDevelopmentDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<HagaleDbContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        if (string.Equals(database.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal) ||
            database.Database.IsNpgsql())
        {
            await database.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await database.Database.MigrateAsync(cancellationToken);
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var roleName in HagaleRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"No fue posible inicializar el rol {roleName}.");
                }
            }
        }

        if (!await database.PricingRules.AnyAsync(cancellationToken))
        {
            database.PricingRules.Add(new PricingRule(
                cityCode: "BUC",
                serviceType: RideServiceType.Motorcycle,
                minimumFareCop: 3_500,
                baseFareCop: 0,
                farePerKilometerCop: 0,
                farePerMinuteCop: 0,
                isActive: true,
                updatedAtUtc: timeProvider.GetUtcNow()));
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
