using Hagale.Application.Contracts;
using Hagale.Application.Common;
using Hagale.Domain.Rides;

namespace Hagale.Application.Rides;

/// <summary>
/// Chat privado de demostración asociado a una carrera. La autorización se
/// comprueba contra los participantes reales de la solicitud y persiste cada
/// mensaje para que sobreviva a reinicios de la aplicación.
/// </summary>
public sealed class RideChatService(
    IRideRequestRepository rideRequestRepository,
    IDriverRepository driverRepository,
    IRideChatMessageRepository rideChatMessageRepository,
    IUserDirectory userDirectory,
    TimeProvider timeProvider,
    IRideRealtimeNotifier? realtimeNotifier = null) : IRideChatService
{
    private const int MaximumMessageLength = 500;
    private const int MaximumMessagesPerRide = 200;

    private static readonly HashSet<RideRequestStatus> ActiveChatStatuses =
    [
        RideRequestStatus.Accepted,
        RideRequestStatus.DriverEnRoute,
        RideRequestStatus.DriverArrived,
        RideRequestStatus.InProgress
    ];

    private readonly IRideRealtimeNotifier realtimeNotifier =
        realtimeNotifier ?? NullRideRealtimeNotifier.Instance;

    public async Task<ApplicationResult<IReadOnlyCollection<RideChatMessageDto>>> ListAsync(
        Guid userId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default)
    {
        var participant = await GetParticipantAsync(userId, rideRequestId, cancellationToken);
        if (!participant.IsSuccess)
        {
            return ApplicationResult<IReadOnlyCollection<RideChatMessageDto>>.Failure(participant.Error!);
        }

        var messages = await rideChatMessageRepository.ListByRideRequestIdAsync(
            rideRequestId,
            MaximumMessagesPerRide,
            cancellationToken);
        return ApplicationResult<IReadOnlyCollection<RideChatMessageDto>>.Success(
            messages.Select(ToDto).ToArray());
    }

    public async Task<ApplicationResult<RideChatMessageDto>> SendAsync(
        Guid userId,
        Guid rideRequestId,
        SendRideChatMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var participant = await GetParticipantAsync(userId, rideRequestId, cancellationToken);
        if (!participant.IsSuccess)
        {
            return ApplicationResult<RideChatMessageDto>.Failure(participant.Error!);
        }

        if (!ActiveChatStatuses.Contains(participant.Ride!.Status) || participant.Ride.AssignedDriverProfileId is null)
        {
            return ApplicationResult<RideChatMessageDto>.Failure(
                "El chat se activa cuando el servicio esté aceptado o en curso.");
        }

        var message = command.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return ApplicationResult<RideChatMessageDto>.Failure("Escribe un mensaje antes de enviarlo.");
        }

        if (message.Length > MaximumMessageLength)
        {
            return ApplicationResult<RideChatMessageDto>.Failure(
                $"El mensaje no puede superar {MaximumMessageLength} caracteres.");
        }

        var ride = participant.Ride!;
        var chatMessage = new RideChatMessage(
            ride.Id,
            userId,
            participant.Role!,
            participant.Name!,
            message,
            timeProvider.GetUtcNow());
        rideChatMessageRepository.Add(chatMessage);
        await rideChatMessageRepository.SaveChangesAsync(cancellationToken);

        var driver = await driverRepository.GetByIdAsync(ride.AssignedDriverProfileId.Value, cancellationToken);
        await realtimeNotifier.NotifyRideChatMessageAsync(
            ride.CustomerUserId,
            driver?.UserId,
            ride.Id,
            cancellationToken);

        return ApplicationResult<RideChatMessageDto>.Success(ToDto(chatMessage));
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
            var customerProfile = await userDirectory.GetBasicProfileAsync(userId, cancellationToken);
            return ParticipantResult.Success(
                ride,
                "Cliente",
                DisplayName(customerProfile, "Cliente HÁGALE"));
        }

        if (ride.AssignedDriverProfileId is null)
        {
            return ParticipantResult.Failure("No tienes acceso a este chat privado.");
        }

        var driver = await driverRepository.GetByUserIdAsync(userId, cancellationToken);
        if (driver?.Id != ride.AssignedDriverProfileId.Value)
        {
            return ParticipantResult.Failure("No tienes acceso a este chat privado.");
        }

        var driverProfile = await userDirectory.GetBasicProfileAsync(userId, cancellationToken);
        return ParticipantResult.Success(
            ride,
            "Conductor",
            DisplayName(driverProfile, "Conductor HÁGALE"));
    }

    private static string DisplayName(BasicUserProfile? profile, string fallback)
    {
        var name = $"{profile?.FirstName} {profile?.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private static RideChatMessageDto ToDto(RideChatMessage message) =>
        new(
            message.Id,
            message.RideRequestId,
            message.SenderUserId,
            message.SenderRole,
            message.SenderName,
            message.Message,
            message.SentAtUtc);

    private sealed record ParticipantResult(
        bool IsSuccess,
        RideRequest? Ride,
        string? Role,
        string? Name,
        string? Error)
    {
        public static ParticipantResult Success(RideRequest ride, string role, string name) =>
            new(true, ride, role, name, null);

        public static ParticipantResult Failure(string error) =>
            new(false, null, null, null, error);
    }
}
