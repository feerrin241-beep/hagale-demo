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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        database.SaveChangesAsync(cancellationToken);
}
