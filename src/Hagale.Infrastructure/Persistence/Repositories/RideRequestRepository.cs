using Hagale.Application.Contracts;
using Hagale.Application.Common;
using Hagale.Domain.Rides;
using Microsoft.EntityFrameworkCore;

namespace Hagale.Infrastructure.Persistence.Repositories;

public sealed class RideRequestRepository(HagaleDbContext database) : IRideRequestRepository
{
    public void Add(RideRequest rideRequest) => database.RideRequests.Add(rideRequest);

    public Task<RideRequest?> GetByIdAsync(Guid rideRequestId, CancellationToken cancellationToken = default) =>
        database.RideRequests.SingleOrDefaultAsync(request => request.Id == rideRequestId, cancellationToken);

    public async Task<IReadOnlyCollection<RideRequest>> ListByCustomerUserIdAsync(
        Guid customerUserId,
        CancellationToken cancellationToken = default) =>
        await database.RideRequests
            .AsNoTracking()
            .Where(request => request.CustomerUserId == customerUserId)
            .OrderByDescending(request => request.RequestedAtUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RideRequest>> ListPendingAsync(CancellationToken cancellationToken = default) =>
        await database.RideRequests
            .AsNoTracking()
            .Where(request => request.Status == RideRequestStatus.Pending)
            .OrderBy(request => request.RequestedAtUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<RideRequest>> ListCompletedByDriverProfileIdAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken = default) =>
        await database.RideRequests
            .AsNoTracking()
            .Where(request => request.AssignedDriverProfileId == driverProfileId && request.Status == RideRequestStatus.Completed)
            .OrderByDescending(request => request.CompletedAtUtc)
            .ToArrayAsync(cancellationToken);

    public Task<RideRequest?> GetActiveByDriverProfileIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default) =>
        database.RideRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request => request.AssignedDriverProfileId == driverProfileId &&
                    (request.Status == RideRequestStatus.CounterOfferPending ||
                     request.Status == RideRequestStatus.Accepted ||
                     request.Status == RideRequestStatus.DriverEnRoute ||
                     request.Status == RideRequestStatus.DriverArrived ||
                     request.Status == RideRequestStatus.InProgress),
                cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrentUpdateException("La solicitud fue actualizada por otra operación.", exception);
        }
    }
}
