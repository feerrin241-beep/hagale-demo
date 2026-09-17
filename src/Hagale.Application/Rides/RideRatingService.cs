using Hagale.Application.Common;
using Hagale.Application.Contracts;
using Hagale.Domain.Rides;

namespace Hagale.Application.Rides;

/// <summary>
/// Reputación bilateral. Cada participante puede calificar una sola vez cuando
/// la carrera finaliza y las calificaciones quedan guardadas de forma persistente.
/// </summary>
public sealed class RideRatingService(
    IRideRequestRepository rideRequestRepository,
    IDriverRepository driverRepository,
    IRideRatingRepository rideRatingRepository,
    TimeProvider timeProvider) : IRideRatingService
{
    private const int MaximumCommentLength = 240;
    private static readonly TimeZoneInfo ColombiaTimeZone = ResolveColombiaTimeZone();

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

        var ratings = await rideRatingRepository.ListByRideRequestIdAsync(
            rideRequestId,
            cancellationToken);

        // La propia nota se confirma de inmediato. La recibida solo se revela
        // desde el día siguiente en Colombia y sin datos de identidad.
        return ApplicationResult<IReadOnlyCollection<RideRatingDto>>.Success(
            ratings
                .Where(rating => rating.RaterUserId == userId || IsVisibleToRecipient(rating))
                .Select(rating => ToDto(rating, rating.RaterUserId == userId))
                .ToArray());
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

        if (await rideRatingRepository.ExistsByRideAndRaterAsync(
                ride.Id,
                userId,
                participant.RaterRole!,
                cancellationToken))
        {
            return ApplicationResult<RideRatingDto>.Failure("Ya calificaste este servicio.");
        }

        var rating = new RideRating(
            ride.Id,
            userId,
            participant.RaterRole!,
            participant.RatedRole!,
            command.Score,
            comment,
            timeProvider.GetUtcNow());
        rideRatingRepository.Add(rating);
        await rideRatingRepository.SaveChangesAsync(cancellationToken);

        return ApplicationResult<RideRatingDto>.Success(ToDto(rating, isMine: true));
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

    private bool IsVisibleToRecipient(RideRating rating)
    {
        var ratingDay = TimeZoneInfo.ConvertTime(rating.RatedAtUtc, ColombiaTimeZone).Date;
        var currentDay = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ColombiaTimeZone).Date;
        return currentDay > ratingDay;
    }

    private static RideRatingDto ToDto(RideRating rating, bool isMine) =>
        new(
            rating.Score,
            isMine ? rating.Comment : null,
            isMine);

    private static TimeZoneInfo ResolveColombiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
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
