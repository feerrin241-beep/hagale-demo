using Hagale.Application.Authentication;
using Hagale.Application.Contracts;
using Hagale.Application.Drivers;
using Hagale.Application.Pricing;
using Hagale.Application.Rides;
using Hagale.Application.Safety;
using Hagale.Infrastructure.Authentication;
using Hagale.Infrastructure.Identity;
using Hagale.Infrastructure.Persistence;
using Hagale.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddDbContext<HagaleDbContext>(options =>
        {
            if (string.Equals(databaseProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                options.UseInMemoryDatabase(connectionString);
                return;
            }

            if (string.Equals(databaseProvider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(databaseProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString);
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
}
