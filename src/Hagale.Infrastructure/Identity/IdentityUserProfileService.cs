using Hagale.Application.Authentication;
using Hagale.Application.Common;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace Hagale.Infrastructure.Identity;

public sealed class IdentityUserProfileService(UserManager<AppUser> userManager) : IUserProfileService
{
    private static readonly Regex E164PhoneNumber = new("^\\+[1-9]\\d{7,14}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public async Task<ApplicationResult<UserProfileDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null || !user.IsActive
            ? ApplicationResult<UserProfileDto>.Failure("La cuenta no está disponible.")
            : ApplicationResult<UserProfileDto>.Success(await MapAsync(user));
    }

    public async Task<ApplicationResult<UserProfileDto>> UpdateAsync(
        Guid userId,
        UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return ApplicationResult<UserProfileDto>.Failure("La cuenta no está disponible.");
        }

        var firstName = command.FirstName?.Trim();
        var lastName = command.LastName?.Trim();
        var phoneNumber = command.PhoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 100 ||
            string.IsNullOrWhiteSpace(lastName) || lastName.Length > 100 ||
            string.IsNullOrWhiteSpace(phoneNumber) || !E164PhoneNumber.IsMatch(phoneNumber))
        {
            return ApplicationResult<UserProfileDto>.Failure("Los datos del perfil no son válidos.");
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.PhoneNumber = phoneNumber;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? ApplicationResult<UserProfileDto>.Success(await MapAsync(user))
            : ApplicationResult<UserProfileDto>.Failure("No fue posible actualizar el perfil.");
    }


    public async Task<ApplicationResult<bool>> DeactivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return ApplicationResult<bool>.Failure("La cuenta no está disponible.");
        }

        user.IsActive = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? ApplicationResult<bool>.Success(true)
            : ApplicationResult<bool>.Failure("No fue posible desactivar la cuenta.");
    }
    private async Task<UserProfileDto> MapAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserProfileDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty,
            user.RegisteredAtUtc,
            user.LastActivityAtUtc,
            roles.ToArray());
    }
}
