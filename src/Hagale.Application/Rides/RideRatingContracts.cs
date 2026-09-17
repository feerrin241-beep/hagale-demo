using Hagale.Application.Common;

namespace Hagale.Application.Rides;

public sealed record SubmitRideRatingCommand(int Score, string? Comment);

// La identidad de quien califica nunca sale del servidor. La persona evaluada
// recibe únicamente una nota anónima desde el día calendario siguiente.
public sealed record RideRatingDto(
    int Score,
    string? Comment,
    bool IsMine);

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
