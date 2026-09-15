using System.Collections.Concurrent;
using Hagale.Application.Common;
using Hagale.Application.Contracts;
using Hagale.Domain.Rides;

namespace Hagale.Application.Rides;

/// <summary>
/// Reputación bilateral para la demo. Cada participante puede calificar una
/// sola vez cuando la carrera finaliza; más adelante se reemplaza por tablas
/// persistentes y moderación.
/// </summary>
public sealed class RideRatingService(
    IRideRequestRepository rideRequestRepository,
    IDriverRepository driverRepository,
    TimeProvider timeProvider) : IRideRatingService
{
    private const int MaximumCommentLength = 240;

    private static readonly ConcurrentDictionary<Guid, List<RideRatingDto>> Ratings = new();
    private static readonly ConcurrentDictionary<Guid, object> RatingLocks = new();

    public async Task<ApplicationResult<IReadOnlyCollection<RideRatingDto>>> ListAsync(
        Guid userId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        var participant = await GetParticipantAsync(userId, rideRequestId, cancellationToken);
        if (!participant.IsSuccess)
        {
            return ApplicationResult<IReadOnlyCollection<RideRatingDto>>.Failure(participant.Error!);
        }

        var ratings = Ratings.TryGetValue(rideRequestId, out var stored)
            ? SnapshotRatings(rideRequestId, stored)
            : Array.Empty<RideRatingDto>();

        return ApplicationResult<IReadOnlyCollection<RideRatingDto>>.Success(ratings);
    }

    public async Task<ApplicationResult<RideRatingDto>> SubmitAsync(
        Guid userId,
        Guid rideRequestId,
        SubmitRideRatingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var participant = await GetParticipantAsync(userId, rideRequestId, cancellationToken);
        if (!participant.IsSuccess)
        {
            return ApplicationResult<RideRatingDto>.Failure(participant.Error!);
        }

        var ride = participant.Ride!;
        if (ride.Status != RideRequestStatus.Completed)
        {
            return ApplicationResult<RideRatingDto>.Failure(
                "La calificación se habilita cuando el servicio esté finalizado.");
        }

        if (command.Score is < 1 or > 5)
        {
            return ApplicationResult<RideRatingDto>.Failure("La calificación debe estar entre 1 y 5 estrellas.");
        }

        var comment = string.IsNullOrWhiteSpace(command.Comment)
            ? null
            : command.Comment.Trim();
        if (comment?.Length > MaximumCommentLength)
        {
            return ApplicationResult<RideRatingDto>.Failure(
                $"El comentario no puede superar {MaximumCommentLength} caracteres.");
        }

        var rating = new RideRatingDto(
            Guid.NewGuid(),
            ride.Id,
            userId,
            participant.RaterRole!,
            participant.RatedRole!,
            command.Score,
            comment,
            timeProvider.GetUtcNow());

        var ratings = Ratings.GetOrAdd(rideRequestId, _ => []);
        lock (RatingLocks.GetOrAdd(rideRequestId, _ => new object()))
        {
            if (ratings.Any(item => item.RaterUserId == userId && item.RaterRole == participant.RaterRole))
            {
                return ApplicationResult<RideRatingDto>.Failure("Ya calificaste este servicio.");
            }

            ratings.Add(rating);
        }

        return ApplicationResult<RideRatingDto>.Success(rating);
    }

    private async Task<ParticipantResult> GetParticipantAsync(
        Guid userId,
        Guid rideRequestId,
        CancellationToken cancellationToken)
    {
        var ride = await rideRequestRepository.GetByIdAsync(rideRequestId, cancellationToken);
        if (ride is null)
        {
            return ParticipantResult.Failure("El servicio no existe.");
        }

        if (ride.CustomerUserId == userId)
        {
            return ParticipantResult.Success(ride, "Customer", "Driver");
        }

        if (ride.AssignedDriverProfileId is null)
        {
            return ParticipantResult.Failure("No tienes acceso a esta calificación.");
        }

        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken);
        if (driver?.Id != ride.AssignedDriverProfileId.Value)
        {
            return ParticipantResult.Failure("No tienes acceso a esta calificación.");
        }

        return ParticipantResult.Success(ride, "Driver", "Customer");
    }

    private static IReadOnlyCollection<RideRatingDto> SnapshotRatings(
        Guid rideRequestId,
        List<RideRatingDto> ratings)
    {
        lock (RatingLocks.GetOrAdd(rideRequestId, _ => new object()))
        {
            return ratings
                .OrderBy(rating => rating.RatedAtUtc)
                .ToArray();
        }
    }

    private sealed record ParticipantResult(
        bool IsSuccess,
        RideRequest? Ride,
        string? RaterRole,
        string? RatedRole,
        string? Error)
    {
        public static ParticipantResult Success(RideRequest ride, string raterRole, string ratedRole) =>
            new(true, ride, raterRole, ratedRole, null);

        public static ParticipantResult Failure(string error) =>
            new(false, null, null, null, error);
    }
}
