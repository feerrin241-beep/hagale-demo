using Hagale.Application.Authentication;
using Hagale.Domain.Pricing;
using Hagale.Domain.Platform;
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

        if (database.Database.IsNpgsql())
        {
            await database.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "StoredDocumentBlobs" (
                    "StorageObjectKey" character varying(500) NOT NULL,
                    "OwnerUserId" uuid NOT NULL,
                    "ContentType" character varying(120) NOT NULL,
                    "Content" bytea NOT NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_StoredDocumentBlobs" PRIMARY KEY ("StorageObjectKey")
                );
                CREATE INDEX IF NOT EXISTS "IX_StoredDocumentBlobs_OwnerUserId"
                    ON "StoredDocumentBlobs" ("OwnerUserId");
                """, cancellationToken);
            await database.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "PlatformAppearances" (
                    "Id" integer NOT NULL,
                    "AccentColor" character varying(16) NOT NULL,
                    "ActionColor" character varying(16) NOT NULL,
                    "BusyColor" character varying(16) NOT NULL,
                    "CustomerModeLabel" character varying(40) NOT NULL,
                    "DriverModeLabel" character varying(40) NOT NULL,
                    "FreeStatusLabel" character varying(40) NOT NULL,
                    "BusyStatusLabel" character varying(40) NOT NULL,
                    "RequestActionLabel" character varying(80) NOT NULL,
                    "DriverOfferVoiceTemplate" character varying(500) NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_PlatformAppearances" PRIMARY KEY ("Id")
                );
                """, cancellationToken);
            await database.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "PricingRules"
                    ADD COLUMN IF NOT EXISTS "FairOfferMinimumPercent" integer NOT NULL DEFAULT 90;
                ALTER TABLE "PricingRules"
                    ADD COLUMN IF NOT EXISTS "FavorableOfferMinimumPercent" integer NOT NULL DEFAULT 105;
                """, cancellationToken);
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


        if (!await database.PlatformAppearances.AnyAsync(cancellationToken))
        {
            database.PlatformAppearances.Add(new PlatformAppearance(timeProvider.GetUtcNow()));
            await database.SaveChangesAsync(cancellationToken);
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
