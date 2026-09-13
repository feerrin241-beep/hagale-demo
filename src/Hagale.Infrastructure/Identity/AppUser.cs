using Microsoft.AspNetCore.Identity;

namespace Hagale.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateTimeOffset RegisteredAtUtc { get; set; }
    public DateTimeOffset? LastActivityAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
