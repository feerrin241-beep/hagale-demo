using System.Security.Claims;

namespace Hagale.Api.Controllers;

internal static class CurrentUserExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("El token no contiene una identidad válida.");
    }
}
