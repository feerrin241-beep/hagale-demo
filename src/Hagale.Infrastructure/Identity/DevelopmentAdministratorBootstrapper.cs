using Hagale.Application.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hagale.Infrastructure.Identity;

public static class DevelopmentAdministratorBootstrapper
{
    private const string DemoAdministratorEmail = "ferrin241@gmail.com";
    private const string DemoAdministratorPassword = "HagaleDemo2026!";

    public static async Task EnsureConfiguredAdministratorAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var hostEnvironment = services.GetRequiredService<IHostEnvironment>();
        var email = configuration["DevelopmentBootstrap:AdministratorEmail"]?.Trim();
        var password = configuration["DevelopmentBootstrap:AdministratorPassword"];

        // Render usa una base InMemory para la demo gratuita. Cuando el
        // servicio se reinicia, la cuenta se pierde; por eso la demo necesita
        // una cuenta conocida que se reconstruya automáticamente. En
        // Development/Production seguimos exigiendo secretos configurados.
        if (hostEnvironment.IsEnvironment("Demo")
            && string.IsNullOrWhiteSpace(email)
            && string.IsNullOrWhiteSpace(password))
        {
            email = DemoAdministratorEmail;
            password = DemoAdministratorPassword;
        }

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
        else if (hostEnvironment.IsEnvironment("Demo")
                 && !await userManager.CheckPasswordAsync(administrator, password))
        {
            // La demo puede conservar una cuenta creada con una clave vieja
            // durante una prueba. La dejamos alineada con la clave pública de
            // acceso de demostración y desbloqueada.
            if (await userManager.HasPasswordAsync(administrator))
            {
                var removeResult = await userManager.RemovePasswordAsync(administrator);
                if (!removeResult.Succeeded)
                {
                    throw new InvalidOperationException("No fue posible actualizar la contraseña del administrador demo.");
                }
            }

            var addResult = await userManager.AddPasswordAsync(administrator, password);
            if (!addResult.Succeeded)
            {
                throw new InvalidOperationException("No fue posible actualizar la contraseña del administrador demo.");
            }

            administrator.LockoutEnd = null;
            administrator.AccessFailedCount = 0;
            administrator.EmailConfirmed = true;
            await userManager.UpdateAsync(administrator);
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
