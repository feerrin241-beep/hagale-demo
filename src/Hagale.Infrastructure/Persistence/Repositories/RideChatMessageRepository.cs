using Hagale.Application.Contracts;
using Hagale.Domain.Rides;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class RideChatMessageRepository(HagaleDbContext database) : IRideChatMessageRepository
{
    public void Add(RideChatMessage message) => database.RideChatMessages.Add(message);

    public async Task<IReadOnlyCollection<RideChatMessage>> ListByRideRequestIdAsync(
        Guid rideRequestId,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        var recentMessages = await database.RideChatMessages
            .AsNoTracking()
            .Where(message => message.RideRequestId == rideRequestId)
            .OrderByDescending(message => message.SentAtUtc)
            .Take(maximumCount)
            .ToArrayAsync(cancellationToken);

        return recentMessages.OrderBy(message => message.SentAtUtc).ToArray();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        database.SaveChangesAsync(cancellationToken);
}
