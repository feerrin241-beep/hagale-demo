using Hagale.Application.Common;

namespace Hagale.Application.Rides;

public sealed record SubmitRideRatingCommand(int Score, string? Comment);

public sealed record RideRatingDto(
    Guid Id,
    Guid RideRequestId,
    Guid RaterUserId,
    string RaterRole,
    string RatedRole,
    int Score,
    string? Comment,
    DateTimeOffset RatedAtUtc);

public interface IRideRatingService
{
    Task<ApplicationResult<IReadOnlyCollection<RideRatingDto>>> ListAsync(
        Guid userId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<RideRatingDto>> SubmitAsync(
        Guid userId,
        Guid rideRequestId,
        SubmitRideRatingCommand command,
        CancellationToken cancellationToken = default);
}
