using Hagale.Domain.Rides;

namespace Hagale.Application.Contracts;

public interface IRideChatMessageRepository
{
    Task<IReadOnlyCollection<RideChatMessage>> ListByRideRequestIdAsync(
        Guid rideRequestId,
        int maximumCount,
        CancellationToken cancellationToken = default);

    void Add(RideChatMessage message);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
