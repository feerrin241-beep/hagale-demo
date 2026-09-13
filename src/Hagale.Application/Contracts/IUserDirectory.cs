namespace Hagale.Application.Contracts;

public interface IUserDirectory
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> EnsureRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<BasicUserProfile?> GetBasicProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<BasicUserProfile?>(null);
}

public sealed record BasicUserProfile(string FirstName, string LastName);
