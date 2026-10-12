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

    Task<DriverRatingSummary> GetDriverSummaryAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DriverRatingSummary(null, 0));

    void Add(RideRating rating);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record DriverRatingSummary(decimal? AverageScore, int RatingCount);
