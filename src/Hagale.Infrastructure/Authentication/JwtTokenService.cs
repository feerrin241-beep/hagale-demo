using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hagale.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hagale.Infrastructure.Authentication;

public sealed class JwtTokenService(
    UserManager<AppUser> userManager,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IJwtTokenService
{
    public async Task<GeneratedAccessToken> CreateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var configuration = options.Value;
        Validate(configuration);

        var roles = await userManager.GetRolesAsync(user);
        var now = timeProvider.GetUtcNow();
        var expiration = now.AddMinutes(configuration.AccessTokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.SigningKey)),
            SecurityAlgorithms.HmacSha512);

        var token = new JwtSecurityToken(
            configuration.Issuer,
            configuration.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: expiration.UtcDateTime,
            signingCredentials: credentials);

        return new GeneratedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiration, roles.ToArray());
    }

    private static void Validate(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("La configuración JWT requiere emisor y audiencia.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey) || Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException("La llave de firma JWT debe configurarse fuera del código y tener al menos 32 bytes.");
        }

        if (options.AccessTokenLifetimeMinutes is < 5 or > 60)
        {
            throw new InvalidOperationException("La vigencia del token JWT debe estar entre 5 y 60 minutos.");
        }
    }
}
