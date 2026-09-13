using Hagale.Application.Authentication;
using Hagale.Application.Common;
using Hagale.Infrastructure.Authentication;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hagale.Infrastructure.Identity;

public sealed class IdentityAuthenticationService(
    UserManager<AppUser> userManager,
    IJwtTokenService tokenService,
    IOptions<GoogleAuthenticationOptions> googleOptions,
    TimeProvider timeProvider,
    ILogger<IdentityAuthenticationService> logger,
    IHostEnvironment hostEnvironment) : IAuthenticationService
{
    private const string GoogleLoginProvider = "Google";

    public async Task<ApplicationResult<AuthenticatedUserDto>> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = command.Email.Trim(),
            Email = command.Email.Trim(),
            PhoneNumber = command.PhoneNumber.Trim(),
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName.Trim(),
            RegisteredAtUtc = timeProvider.GetUtcNow(),
            LastActivityAtUtc = timeProvider.GetUtcNow(),
            IsActive = true,
            LockoutEnabled = true
        };

        try
        {
            var createResult = await userManager.CreateAsync(user, command.Password);
            if (!createResult.Succeeded)
            {
                return ApplicationResult<AuthenticatedUserDto>.Failure(BuildIdentityErrorMessage(
                    createResult,
                    "No fue posible crear la cuenta."));
            }

            var roleResult = await userManager.AddToRoleAsync(user, HagaleRoles.Customer);
            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(user);
                return ApplicationResult<AuthenticatedUserDto>.Failure(BuildIdentityErrorMessage(
                    roleResult,
                    "No fue posible preparar la cuenta."));
            }

            try
            {
                return ApplicationResult<AuthenticatedUserDto>.Success(
                    await CreateAuthenticatedUserAsync(user, cancellationToken));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "No fue posible emitir la sesión después de registrar al usuario {Email}.", user.Email);
                await userManager.DeleteAsync(user);
                return ApplicationResult<AuthenticatedUserDto>.Failure(BuildRegistrationExceptionMessage(exception));
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error inesperado durante el registro de {Email}.", user.Email);
            return ApplicationResult<AuthenticatedUserDto>.Failure(BuildRegistrationExceptionMessage(exception));
        }
    }

    public async Task<ApplicationResult<AuthenticatedUserDto>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            return ApplicationResult<AuthenticatedUserDto>.Failure("Correo o contraseña incorrectos.");
        }

        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            return ApplicationResult<AuthenticatedUserDto>.Failure("Correo o contraseña incorrectos.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastActivityAtUtc = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);
        return ApplicationResult<AuthenticatedUserDto>.Success(await CreateAuthenticatedUserAsync(user, cancellationToken));
    }

    public ExternalAuthProviderStatusDto GetGoogleProviderStatus()
    {
        var clientId = googleOptions.Value.ClientId?.Trim();
        return new ExternalAuthProviderStatusDto(
            GoogleLoginProvider,
            !string.IsNullOrWhiteSpace(clientId),
            string.IsNullOrWhiteSpace(clientId) ? null : clientId);
    }

    public async Task<ApplicationResult<AuthenticatedUserDto>> SignInWithGoogleAsync(
        GoogleSignInCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var clientId = googleOptions.Value.ClientId?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return ApplicationResult<AuthenticatedUserDto>.Failure("El ingreso con Google aún no está configurado para HÁGALE.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                command.Credential,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [clientId]
                });
        }
        catch (Exception exception) when (exception is InvalidJwtException or ArgumentException)
        {
            return ApplicationResult<AuthenticatedUserDto>.Failure("Google no pudo validar esta sesión. Inténtalo nuevamente.");
        }

        if (payload.EmailVerified != true || string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Subject))
        {
            return ApplicationResult<AuthenticatedUserDto>.Failure("Google no confirmó un correo válido para esta cuenta.");
        }

        var email = payload.Email.Trim();
        var user = await userManager.FindByLoginAsync(GoogleLoginProvider, payload.Subject)
            ?? await userManager.FindByEmailAsync(email);
        var isNewUser = user is null;

        if (isNewUser)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = NormalizeExternalName(payload.GivenName, payload.Name, fallback: "Usuario"),
                LastName = NormalizeExternalName(payload.FamilyName, null, fallback: "HÁGALE"),
                RegisteredAtUtc = timeProvider.GetUtcNow(),
                LastActivityAtUtc = timeProvider.GetUtcNow(),
                IsActive = true,
                LockoutEnabled = true
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return ApplicationResult<AuthenticatedUserDto>.Failure("No fue posible crear la cuenta con Google.");
            }

            var roleResult = await userManager.AddToRoleAsync(user, HagaleRoles.Customer);
            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(user);
                return ApplicationResult<AuthenticatedUserDto>.Failure("No fue posible preparar la cuenta con Google.");
            }
        }

        if (!user!.IsActive || await userManager.IsLockedOutAsync(user))
        {
            return ApplicationResult<AuthenticatedUserDto>.Failure("La cuenta no está disponible.");
        }

        var existingLogin = await userManager.FindByLoginAsync(GoogleLoginProvider, payload.Subject);
        if (existingLogin is null)
        {
            var loginResult = await userManager.AddLoginAsync(
                user,
                new UserLoginInfo(GoogleLoginProvider, payload.Subject, "Google"));
            if (!loginResult.Succeeded)
            {
                if (isNewUser)
                {
                    await userManager.DeleteAsync(user);
                }

                return ApplicationResult<AuthenticatedUserDto>.Failure("No fue posible vincular la cuenta de Google.");
            }
        }

        if (!await userManager.IsInRoleAsync(user, HagaleRoles.Customer))
        {
            var roleResult = await userManager.AddToRoleAsync(user, HagaleRoles.Customer);
            if (!roleResult.Succeeded)
            {
                return ApplicationResult<AuthenticatedUserDto>.Failure("No fue posible preparar el acceso de cliente.");
            }
        }

        user.EmailConfirmed = true;
        user.LastActivityAtUtc = timeProvider.GetUtcNow();
        await userManager.UpdateAsync(user);
        return ApplicationResult<AuthenticatedUserDto>.Success(await CreateAuthenticatedUserAsync(user, cancellationToken));
    }

    // El acceso se renueva desde una sesión JWT aún válida. Esto permite que
    // una cuenta aprobada reciba su nuevo rol sin pedir la contraseña otra vez.
    // No admite una cuenta inactiva ni sustituye el inicio de sesión cuando el
    // token original ya venció.
    public async Task<ApplicationResult<AuthenticatedUserDto>> RenewCurrentSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return ApplicationResult<AuthenticatedUserDto>.Failure("La sesión ya no está disponible.");
        }

        user.LastActivityAtUtc = timeProvider.GetUtcNow();
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return ApplicationResult<AuthenticatedUserDto>.Failure("No fue posible renovar la sesión.");
        }

        return ApplicationResult<AuthenticatedUserDto>.Success(await CreateAuthenticatedUserAsync(user, cancellationToken));
    }

    private async Task<AuthenticatedUserDto> CreateAuthenticatedUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        var token = await tokenService.CreateAsync(user, cancellationToken);
        return new AuthenticatedUserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            token.Roles,
            token.Value,
            token.ExpiresAtUtc);
    }

    private static string NormalizeExternalName(string? preferred, string? alternative, string fallback)
    {
        var normalized = preferred?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = alternative?.Trim();
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = fallback;
        }

        return normalized.Length > 100 ? normalized[..100] : normalized;
    }

    private string BuildRegistrationExceptionMessage(Exception exception)
    {
        if (hostEnvironment.IsEnvironment("Demo"))
        {
            // La demo necesita mostrar una causa accionable para poder corregir
            // la configuración del servicio gratuito sin exponerla en producción.
            return $"No fue posible completar el registro de la demo. Detalle técnico: {exception.GetBaseException().Message}";
        }

        return "No fue posible completar el registro. Inténtalo nuevamente.";
    }

    private static string BuildIdentityErrorMessage(IdentityResult result, string fallback)
    {
        var messages = result.Errors
            .Select(error => error.Code switch
            {
                "PasswordTooShort" => "la contraseña debe tener mínimo 12 caracteres",
                "PasswordRequiresUpper" => "la contraseña debe incluir una mayúscula",
                "PasswordRequiresLower" => "la contraseña debe incluir una minúscula",
                "PasswordRequiresDigit" => "la contraseña debe incluir un número",
                "PasswordRequiresNonAlphanumeric" => "la contraseña debe incluir un símbolo",
                "DuplicateUserName" or "DuplicateEmail" => "ese correo ya está registrado",
                _ => error.Description
            })
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return messages.Length == 0
            ? fallback
            : $"{fallback} Revisa: {string.Join("; ", messages)}.";
    }
}
