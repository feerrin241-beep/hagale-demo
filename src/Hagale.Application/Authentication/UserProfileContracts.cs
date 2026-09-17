using Hagale.Application.Common;

namespace Hagale.Application.Authentication;

public sealed record UserProfileDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? LastActivityAtUtc,
    IReadOnlyCollection<string> Roles);

public sealed record UpdateUserProfileCommand(string FirstName, string LastName, string PhoneNumber);

public interface IUserProfileService
{
    Task<ApplicationResult<UserProfileDto>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApplicationResult<UserProfileDto>> UpdateAsync(Guid userId, UpdateUserProfileCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<bool>> DeactivateAsync(Guid userId, CancellationToken cancellationToken = default);
}
