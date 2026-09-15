using Hagale.Application.Common;

namespace Hagale.Application.Rides;

public sealed record SendRideChatMessageCommand(string Message);

public sealed record RideChatMessageDto(
    Guid Id,
    Guid RideRequestId,
    Guid SenderUserId,
    string SenderRole,
    string SenderName,
    string Message,
    DateTimeOffset SentAtUtc);

public interface IRideChatService
{
    Task<ApplicationResult<IReadOnlyCollection<RideChatMessageDto>>> ListAsync(
        Guid userId,
        Guid rideRequestId,
        CancellationToken cancellationToken = default);

    Task<ApplicationResult<RideChatMessageDto>> SendAsync(
        Guid userId,
        Guid rideRequestId,
        SendRideChatMessageCommand command,
        CancellationToken cancellationToken = default);
}
