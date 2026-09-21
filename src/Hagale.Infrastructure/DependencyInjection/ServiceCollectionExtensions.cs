using Hagale.Application.Authentication;
using Hagale.Application.Contracts;
using Hagale.Application.Drivers;
using Hagale.Application.Pricing;
using Hagale.Application.Rides;
using Hagale.Application.Routing;
using Hagale.Application.Safety;
using Hagale.Infrastructure.Authentication;
using Hagale.Infrastructure.Identity;
using Hagale.Infrastructure.Persistence;
using Hagale.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Hagale.Infrastructure.Routing;

namespace Hagale.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHagaleInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HagaleDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configura ConnectionStrings:HagaleDatabase fuera del código fuente.");
        }

        var databaseProvider = configuration["Database:Provider"]?.Trim();
        var usesPostgresConnectionUri = IsPostgresConnectionUri(connectionString);
        services.AddDbContext<HagaleDbContext>(options =>
        {
            if (string.Equals(databaseProvider, "InMemory", StringComparison.OrdinalIgnoreCase) &&
                !usesPostgresConnectionUri)
            {
                options.UseInMemoryDatabase(connectionString);
                return;
            }

            if (string.Equals(databaseProvider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(databaseProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                usesPostgresConnectionUri)
            {
                options.UseNpgsql(NormalizePostgresConnectionString(connectionString));
                return;
            }

            options.UseSqlServer(connectionString);
        });
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<HagaleDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleAuthenticationOptions>(configuration.GetSection(GoogleAuthenticationOptions.SectionName));
        services.Configure<DocumentStorageOptions>(configuration.GetSection(DocumentStorageOptions.SectionName));
        services.Configure<RoadRoutingOptions>(configuration.GetSection(RoadRoutingOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<HttpClient>();
        services.AddTransient<IRoadRoutingService, OsrmRoadRoutingService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IUserProfileService, IdentityUserProfileService>();
        services.AddScoped<IUserDirectory, IdentityUserDirectory>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IRideRequestRepository, RideRequestRepository>();
        services.AddScoped<IRideChatMessageRepository, RideChatMessageRepository>();
        services.AddScoped<IRideRatingRepository, RideRatingRepository>();
        services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();
        services.AddScoped<IEmergencyContactRepository, EmergencyContactRepository>();
        services.AddScoped<IEmergencyServiceChannelRepository, EmergencyServiceChannelRepository>();
        services.AddScoped<IPrivateDocumentStorage, LocalPrivateDocumentStorage>();
        services.AddScoped<IDriverApplicationService, DriverApplicationService>();
        services.AddScoped<IRideRequestService, RideRequestService>();
        services.AddScoped<IRideChatService, RideChatService>();
        services.AddScoped<IRideRatingService, RideRatingService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IEmergencyContactService, EmergencyContactService>();
        services.AddScoped<IEmergencyServiceChannelService, EmergencyServiceChannelService>();
        services.AddHealthChecks().AddDbContextCheck<HagaleDbContext>();

        return services;
    }

    private static bool IsPostgresConnectionUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var connectionUri) &&
        (string.Equals(connectionUri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(connectionUri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Neon displays libpq-style connection URIs. Npgsql and EF Core use the
    /// standard keyword format, so we convert the URI without logging it.
    /// </summary>
    private static string NormalizePostgresConnectionString(string value)
    {
        if (!IsPostgresConnectionUri(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var connectionUri))
        {
            return value;
        }

        var userInfo = Uri.UnescapeDataString(connectionUri.UserInfo);
        var separatorIndex = userInfo.IndexOf(':', StringComparison.Ordinal);
        var username = separatorIndex < 0 ? userInfo : userInfo[..separatorIndex];
        var password = separatorIndex < 0 ? string.Empty : userInfo[(separatorIndex + 1)..];
        var databaseName = Uri.UnescapeDataString(connectionUri.AbsolutePath.Trim('/'));

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = connectionUri.Host,
            Port = connectionUri.IsDefaultPort ? 5432 : connectionUri.Port,
            Database = databaseName,
            Username = username,
            Password = password
        };
        builder["SSL Mode"] = "Require";

        return builder.ConnectionString;
    }
}
