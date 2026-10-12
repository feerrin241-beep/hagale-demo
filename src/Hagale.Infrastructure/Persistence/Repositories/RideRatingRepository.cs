using Hagale.Application.Contracts;
using Hagale.Domain.Rides;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class RideRatingRepository(HagaleDbContext database) : IRideRatingRepository
{
    public void Add(RideRating rating) => database.RideRatings.Add(rating);

    public Task<bool> ExistsByRideAndRaterAsync(
        Guid rideRequestId,
        Guid raterUserId,
        string raterRole,
        CancellationToken cancellationToken = default) =>
        database.RideRatings.AnyAsync(
            rating => rating.RideRequestId == rideRequestId &&
                rating.RaterUserId == raterUserId &&
                rating.RaterRole == raterRole,
            cancellationToken);

    public async Task<IReadOnlyCollection<RideRating>> ListByRideRequestIdAsync(
        Guid rideRequestId,
        CancellationToken cancellationToken = default) =>
        await database.RideRatings
            .AsNoTracking()
            .Where(rating => rating.RideRequestId == rideRequestId)
            .OrderBy(rating => rating.RatedAtUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<DriverRatingSummary> GetDriverSummaryAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken = default)
    {
        var scores = await (
            from rating in database.RideRatings.AsNoTracking()
            join ride in database.RideRequests.AsNoTracking() on rating.RideRequestId equals ride.Id
            where ride.AssignedDriverProfileId == driverProfileId && rating.RatedRole == "Driver"
            select rating.Score
        ).ToArrayAsync(cancellationToken);

        return scores.Length == 0
            ? new DriverRatingSummary(null, 0)
            : new DriverRatingSummary(
                decimal.Round(scores.Average(score => (decimal)score), 1, MidpointRounding.AwayFromZero),
                scores.Length);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        database.SaveChangesAsync(cancellationToken);
}
