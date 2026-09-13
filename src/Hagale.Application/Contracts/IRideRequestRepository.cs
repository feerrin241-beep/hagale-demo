using Hagale.Domain.Rides;

namespace Hagale.Application.Contracts;

public interface IRideRequestRepository
{
    Task<RideRequest?> GetByIdAsync(Guid rideRequestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RideRequest>> ListByCustomerUserIdAsync(Guid customerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RideRequest>> ListPendingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<RideRequest>> ListCompletedByDriverProfileIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default);
    Task<RideRequest?> GetActiveByDriverProfileIdAsync(Guid driverProfileId, CancellationToken cancellationToken = default);
    void Add(RideRequest rideRequest);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
