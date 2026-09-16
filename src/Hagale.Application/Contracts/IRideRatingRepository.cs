using Hagale.Domain.Rides;

namespace Hagale.Application.Contracts;

public interface IRideRatingRepository
{
    Task<IReadOnlyCollection<RideRating>> ListByRideRequestIdAsync(
        Guid rideRequestId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByRideAndRaterAsync(
        Guid rideRequestId,
        Guid raterUserId,
        string raterRole,
        CancellationToken cancellationToken = default);

    void Add(RideRating rating);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
