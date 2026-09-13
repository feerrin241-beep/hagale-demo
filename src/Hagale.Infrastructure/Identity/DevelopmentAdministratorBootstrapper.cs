using Hagale.Application.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hagale.Infrastructure.Identity;

public static class DevelopmentAdministratorBootstrapper
{
    public static async Task EnsureConfiguredAdministratorAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var email = configuration["DevelopmentBootstrap:AdministratorEmail"]?.Trim();
        var password = configuration["DevelopmentBootstrap:AdministratorPassword"];

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("La creación del administrador de desarrollo requiere correo y contraseña configurados como secretos locales.");
        }

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var administrator = await userManager.FindByEmailAsync(email);

        if (administrator is null)
        {
            administrator = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                FirstName = "Administrador",
                LastName = "HÁGALE",
                RegisteredAtUtc = timeProvider.GetUtcNow(),
                LastActivityAtUtc = timeProvider.GetUtcNow(),
                IsActive = true,
                LockoutEnabled = true,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(administrator, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException("No fue posible crear el administrador de desarrollo. Verifica los requisitos de contraseña.");
            }
        }

        if (!await userManager.IsInRoleAsync(administrator, HagaleRoles.Administrator))
        {
            var roleResult = await userManager.AddToRoleAsync(administrator, HagaleRoles.Administrator);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException("No fue posible asignar el rol de administrador de desarrollo.");
            }
        }
    }
}
