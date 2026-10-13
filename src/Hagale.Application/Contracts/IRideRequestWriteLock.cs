using Hagale.Domain.Rides;

namespace Hagale.Application.Contracts;

/// <summary>
/// Holds an exclusive write lock for one ride request until changes are saved
/// or the lock is disposed without committing.
/// </summary>
public interface IRideRequestWriteLock : IAsyncDisposable
{
    RideRequest? Request { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
