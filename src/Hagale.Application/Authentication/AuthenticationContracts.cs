using Hagale.Application.Common;

namespace Hagale.Application.Authentication;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Password);

public sealed record LoginCommand(string Email, string Password);

public sealed record GoogleSignInCommand(string Credential);

public sealed record ExternalAuthProviderStatusDto(
    string Provider,
    bool IsConfigured,
    string? ClientId);

public sealed record AuthenticatedUserDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    IReadOnlyCollection<string> Roles,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc);

public interface IAuthenticationService
{
    Task<ApplicationResult<AuthenticatedUserDto>> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<AuthenticatedUserDto>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);
    ExternalAuthProviderStatusDto GetGoogleProviderStatus();
    Task<ApplicationResult<AuthenticatedUserDto>> SignInWithGoogleAsync(GoogleSignInCommand command, CancellationToken cancellationToken = default);
    Task<ApplicationResult<AuthenticatedUserDto>> RenewCurrentSessionAsync(Guid userId, CancellationToken cancellationToken = default);
}
