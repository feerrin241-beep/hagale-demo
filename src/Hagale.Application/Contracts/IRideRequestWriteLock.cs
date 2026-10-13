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

    /// <summary>
    /// Persists a newly submitted driver offer without updating the parent ride
    /// request row. This avoids optimistic-concurrency conflicts on unrelated
    /// request changes while the offer itself is serialized by the write lock.
    /// </summary>
    Task SaveDriverOfferAsync(RideDriverOffer offer, CancellationToken cancellationToken = default);
}
