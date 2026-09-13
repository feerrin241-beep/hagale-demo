using Hagale.Application.Contracts;
using Hagale.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Infrastructure.Identity;

public sealed class IdentityUserDirectory(HagaleDbContext database) : IUserDirectory
{
    public Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        database.Users.AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);

    public async Task<bool> EnsureRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var role = await database.Roles.SingleOrDefaultAsync(item => item.NormalizedName == roleName.ToUpperInvariant(), cancellationToken);
        if (role is null || !await ExistsAsync(userId, cancellationToken))
        {
            return false;
        }

        var exists = await database.UserRoles.AnyAsync(item => item.UserId == userId && item.RoleId == role.Id, cancellationToken);
        if (!exists)
        {
            database.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>
            {
                UserId = userId,
                RoleId = role.Id
            });
        }

        return true;
    }

    public async Task<BasicUserProfile?> GetBasicProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await database.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && item.IsActive)
            .Select(item => new BasicUserProfile(item.FirstName, item.LastName))
            .SingleOrDefaultAsync(cancellationToken);
        return user;
    }
}
