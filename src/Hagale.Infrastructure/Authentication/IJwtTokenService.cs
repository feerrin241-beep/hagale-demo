using Hagale.Infrastructure.Identity;

namespace Hagale.Infrastructure.Authentication;

public interface IJwtTokenService
{
    Task<GeneratedAccessToken> CreateAsync(AppUser user, CancellationToken cancellationToken = default);
}

public sealed record GeneratedAccessToken(string Value, DateTimeOffset ExpiresAtUtc, IReadOnlyCollection<string> Roles);
